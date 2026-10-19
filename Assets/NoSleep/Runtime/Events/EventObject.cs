using System;
using System.Collections.Generic;
using UnityEngine;

namespace NoSleep
{
    public sealed class EventObject : MonoBehaviour
    {
        GameSession session;
        GameState subscribedState;
        EventRunner subscribedRunner;
        EventDefinition definition;
        string eventKey;
        int pageIndex = -1;
        int failedAutorunPage = -1;
        GameObject visualRoot;
        readonly List<Renderer> renderers = new List<Renderer>();
        readonly List<bool> rendererEnabled = new List<bool>();
        readonly HashSet<Collider> playerColliders = new HashSet<Collider>();
        bool pendingTouch;
        bool npcContact, touchConsumed;

        public string DisplayName => definition == null ? gameObject.name : definition.displayName;
        public int ActivePageIndex => pageIndex;
        public string EventKey => eventKey;
        public EventDefinition Definition => definition;
        public bool AutorunFailed => failedAutorunPage == pageIndex;
        public void MarkAutorunFailed() => failedAutorunPage = pageIndex;
        public bool BlocksMovement => ActivePage != null && ActivePage.blocksMovement;
        public bool CanInteract => CanRun(EventTrigger.Action);
        public EventPage ActivePage => definition == null || definition.pages == null || pageIndex < 0 ||
            pageIndex >= definition.pages.Count ? null : definition.pages[pageIndex];

        public void Configure(GameSession gameSession, EventDefinition eventDefinition, string placementKey)
        {
            session?.Scheduler?.Unregister(this);
            Unsubscribe();
            session = gameSession;
            definition = eventDefinition;
            definition?.EnsureStableIds();
            eventKey = placementKey;
            failedAutorunPage = -1;
            ClearTouch();
            CacheRenderers();
            Subscribe();
            RefreshPage();
        }

        public void SetVisualRoot(GameObject visual)
        {
            if (visual == gameObject) throw new ArgumentException("The event visual must be a child object.");
            if (visual != null && !visual.transform.IsChildOf(transform))
                throw new ArgumentException("The event visual must belong to this event object.");
            visualRoot = visual;
            CacheRenderers();
            RefreshPage();
        }

        public bool TryInteract() => CanRun(EventTrigger.Action) && session.Events.TryRun(definition, eventKey, pageIndex);
        public bool TryTouch() => CanRun(EventTrigger.PlayerTouch) && session.Events.TryRun(definition, eventKey, pageIndex);

        // Moving NPC proximity and physics triggers share one contact edge. A
        // fled/cancelled encounter can only retry after the player leaves it.
        public void ObserveNpcContact(bool inside)
        {
            if (inside && !npcContact && !touchConsumed && ActivePage?.trigger == EventTrigger.PlayerTouch) pendingTouch = true;
            npcContact = inside;
            if (!inside && playerColliders.Count == 0) { pendingTouch = false; touchConsumed = false; }
            TryPendingTouch();
        }

        bool CanRun(EventTrigger trigger)
        {
            var page = ActivePage;
            return isActiveAndEnabled && page != null && page.trigger == trigger && session != null &&
                (trigger == EventTrigger.Autorun || GetComponent<NpcActor>() == null || page.visible || page.blocksMovement) &&
                session.State != null && session.Events != null && !session.IsInputBlocked &&
                !session.Events.IsRunning && !definition.IsPageCompleted(session.State, eventKey, pageIndex) &&
                (trigger != EventTrigger.Autorun || failedAutorunPage != pageIndex);
        }

        void Update()
        {
            // A loaded save can replace GameState while keeping the scene alive.
            if (session != null && subscribedState != session.State) { Subscribe(); RefreshPage(); }
            if (session?.Scheduler != null) { session.Scheduler.Observe(this); return; }
            if (CanRun(EventTrigger.Autorun) && !session.Events.TryRun(definition, eventKey, pageIndex))
                failedAutorunPage = pageIndex;
        }

        void OnTriggerEnter(Collider other)
        {
            if (other.GetComponentInParent<PlayerController>() == null) return;
            playerColliders.RemoveWhere(collider => collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy);
            bool firstEntry = playerColliders.Count == 0;
            playerColliders.Add(other);
            if (firstEntry && !touchConsumed && ActivePage != null && ActivePage.trigger == EventTrigger.PlayerTouch) pendingTouch = true;
            TryPendingTouch();
        }

        void OnTriggerStay(Collider other)
        {
            if (playerColliders.Contains(other)) TryPendingTouch();
        }

        void OnTriggerExit(Collider other)
        {
            playerColliders.Remove(other);
            playerColliders.RemoveWhere(collider => collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy);
            if (playerColliders.Count == 0 && !npcContact) { pendingTouch = false; touchConsumed = false; }
        }

        void TryPendingTouch()
        {
            if (pendingTouch && !touchConsumed && TryTouch()) { pendingTouch = false; touchConsumed = true; }
        }

        void ClearTouch()
        {
            playerColliders.Clear();
            pendingTouch = false;
            npcContact = touchConsumed = false;
        }

        void OnEnable() { Subscribe(); RefreshPage(); }
        void OnDisable() { session?.Scheduler?.Unregister(this); Unsubscribe(); ClearTouch(); }
        void OnDestroy() { session?.Scheduler?.Unregister(this); Unsubscribe(); }

        void Subscribe()
        {
            Unsubscribe();
            if (!isActiveAndEnabled || session == null || session.State == null) return;
            subscribedState = session.State;
            subscribedState.Changed += RefreshPage;
            subscribedRunner = session.Events;
            if (subscribedRunner != null) subscribedRunner.Failed += OnRunnerFailed;
        }

        void Unsubscribe()
        {
            if (subscribedState != null) subscribedState.Changed -= RefreshPage;
            if (subscribedRunner != null) subscribedRunner.Failed -= OnRunnerFailed;
            subscribedState = null;
            subscribedRunner = null;
        }

        void RefreshPage()
        {
            int nextPageIndex = definition == null || session == null || session.State == null ? -1 :
                definition.ResolvePageIndex(session.State, eventKey);
            if (nextPageIndex != pageIndex) failedAutorunPage = -1;
            pageIndex = nextPageIndex;
            session?.Scheduler?.Observe(this);
            if (ActivePage == null || ActivePage.trigger != EventTrigger.PlayerTouch) pendingTouch = false;
            bool visible = ActivePage != null && ActivePage.visible;
            if (visualRoot != null) visualRoot.SetActive(visible);
            else
                for (int index = 0; index < renderers.Count; index++)
                    if (renderers[index] != null) renderers[index].enabled = visible && rendererEnabled[index];
        }

        void OnRunnerFailed(Exception error)
        {
            // A broken autorun must not log and lock the player again on every frame.
            if (subscribedRunner != null && subscribedRunner.CurrentEventKey == eventKey &&
                subscribedRunner.CurrentPageIndex == pageIndex)
            {
                failedAutorunPage = pageIndex;
                pendingTouch = false;
            }
        }

        void CacheRenderers()
        {
            renderers.Clear(); rendererEnabled.Clear();
            if (visualRoot != null) return;
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                renderers.Add(renderer);
                rendererEnabled.Add(renderer.enabled);
            }
        }
    }
}
