using System;
using System.Collections.Generic;
using UnityEngine;

namespace NoSleep
{
    public sealed class ForegroundRequestInfo
    {
        public string EventKey { get; internal set; }
        public string DefinitionName { get; internal set; }
        public string PageId { get; internal set; }
    }

    // Background programs share the interpreter, but never own input, UI or map transfer.
    [DefaultExecutionOrder(100)]
    public sealed class EventScheduler : MonoBehaviour
    {
        sealed class Registration
        {
            public EventObject Owner;
            public string PageId;
            public bool Finished, Faulted, Queued;
            public double NextRun;
            public EventExecutionContext Context;
        }
        sealed class Request
        {
            public EventDefinition Definition;
            public string EventKey, PageId, SourcePageId, Token;
            public EventObject Source;
            public int Generation;
        }
        const int FrameBudget = 128, SliceBudget = 16, QueueLimit = 32;
        readonly Dictionary<string, Registration> registrations = new Dictionary<string, Registration>(StringComparer.Ordinal);
        readonly List<EventExecutionContext> contexts = new List<EventExecutionContext>();
        readonly List<EventExecutionContext> recent = new List<EventExecutionContext>();
        readonly List<Request> queue = new List<Request>();
        GameSession session;
        int generation, cursor;
        double activeSeconds;
        bool pumping, dispatching, stepping;
        public bool Paused { get; private set; }
        public int QueuedCount => queue.Count;
        public IReadOnlyList<EventExecutionContext> BackgroundContexts => contexts;
        public IReadOnlyList<EventExecutionContext> RecentContexts => recent;
        public bool IsAtSafeBoundary => !pumping && !dispatching;
        public string Status => (Paused ? "Paused" : "Running") + "; background=" + contexts.Count + "; queued=" + queue.Count;
        public IEnumerable<ForegroundRequestInfo> PendingRequests
        {
            get
            {
                foreach (var item in queue)
                    yield return new ForegroundRequestInfo { EventKey = item.EventKey, DefinitionName = item.Definition != null ? item.Definition.displayName : "", PageId = item.PageId };
            }
        }
        public void Configure(GameSession gameSession) { CancelAll(); session = gameSession; }
        public void Observe(EventObject owner)
        {
            if (owner == null || string.IsNullOrWhiteSpace(owner.EventKey)) return;
            if (!registrations.TryGetValue(owner.EventKey, out var entry))
                registrations.Add(owner.EventKey, new Registration { Owner = owner });
            else if (entry.Owner != owner)
            {
                if (entry.Context != null) entry.Context.Cancel();
                registrations[owner.EventKey] = new Registration { Owner = owner };
            }
        }
        public void Unregister(EventObject owner)
        {
            if (owner == null || !registrations.TryGetValue(owner.EventKey ?? "", out var entry) || entry.Owner != owner) return;
            // Cancellation is a flag. Iterator disposal happens outside a Changed callback.
            entry.Owner = null;
            if (entry.Context != null) entry.Context.Cancel();
        }
        bool CanAdvance => session != null && session.IsReady && !session.IsGameplayBusy &&
            !session.HasPendingSave && !session.UI.IsModalOpen && session.State.player.health > 0;
        void Update()
        {
            if (session == null || !session.IsReady) return;
            if (CanAdvance && !Paused) activeSeconds += Time.unscaledDeltaTime;
            Reconcile();
            if (Paused || !CanAdvance) return;
            Dispatch();
            if (CanAdvance) Pump(FrameBudget);
        }
        void Reconcile()
        {
            if (session.Map != null) foreach (var owner in session.Map.EventObjects) if (owner != null && owner.isActiveAndEnabled) Observe(owner);
            var keys = new List<string>(registrations.Keys); keys.Sort(StringComparer.Ordinal);
            foreach (string key in keys)
            {
                var entry = registrations[key]; var owner = entry.Owner;
                if (owner == null || !owner.isActiveAndEnabled)
                {
                    Stop(entry); registrations.Remove(key); continue;
                }
                var definition = owner.Definition; var page = owner.ActivePage;
                string pageId = page?.pageId;
                if (entry.PageId != pageId)
                {
                    Stop(entry); entry.PageId = pageId; entry.Finished = entry.Faulted = entry.Queued = false; entry.NextRun = 0;
                }
                if (page == null || definition == null || definition.IsPageCompleted(session.State, key, owner.ActivePageIndex))
                { Stop(entry); continue; }
                if (page.trigger == EventTrigger.Autorun && !entry.Queued && !entry.Faulted && !owner.AutorunFailed && CanAdvance && !Paused)
                    entry.Queued = EnqueueForeground(definition, key, owner.ActivePageIndex);
                if (page.trigger != EventTrigger.Parallel || entry.Context != null || entry.Faulted ||
                    (entry.Finished && !page.parallelRepeat) || activeSeconds < entry.NextRun || !CanAdvance || (Paused && !stepping)) continue;
                try
                {
                    entry.Context = session.Events.CreateExecution(definition, key, owner.ActivePageIndex, true);
                    entry.Context.Paused = Paused; contexts.Add(entry.Context);
                }
                catch (Exception error) { entry.Faulted = true; Debug.LogWarning("Parallel event rejected: " + key + ": " + error.Message, owner); }
            }
            // Completed and cancelled contexts are disposed even while a menu is open.
            for (int i = contexts.Count - 1; i >= 0; i--)
                if (contexts[i].IsComplete || contexts[i].IsCancelled) Finish(contexts[i]);
        }
        void Stop(Registration entry)
        {
            if (entry.Context == null) return;
            entry.Context.Cancel();
            if (!pumping) { contexts.Remove(entry.Context); entry.Context.Dispose(); Remember(entry.Context); entry.Context = null; }
        }
        void Finish(EventExecutionContext context)
        {
            foreach (var entry in registrations.Values)
                if (entry.Context == context)
                {
                    entry.Context = null; entry.Finished = !context.IsCancelled;
                    entry.Faulted = context.Error != null || (!context.Succeeded && !context.IsCancelled);
                    entry.NextRun = activeSeconds + Math.Max(0.01, entry.Owner?.ActivePage?.parallelIntervalSeconds ?? 0.25);
                    if (context.Error != null) Debug.LogWarning("Parallel event failed: " + context.EventKey + ": " + context.Error.Message, entry.Owner);
                    break;
                }
            contexts.Remove(context); context.Dispose(); Remember(context);
        }
        void Remember(EventExecutionContext context)
        {
            if (recent.Contains(context)) return;
            if (recent.Count == 16) recent.RemoveAt(0);
            recent.Add(context);
        }
        public bool EnqueueForeground(EventDefinition definition, string eventKey, int pageIndex = -1)
        {
            if (session == null || definition == null || string.IsNullOrWhiteSpace(eventKey)) return false;
            definition.EnsureStableIds();
            int resolved = definition.ResolvePageIndex(session.State, eventKey);
            if (pageIndex < 0) pageIndex = resolved;
            if (pageIndex != resolved || pageIndex < 0 || definition.pages[pageIndex].trigger == EventTrigger.Parallel ||
                definition.IsPageCompleted(session.State, eventKey, pageIndex)) return false;
            registrations.TryGetValue(eventKey, out var source);
            var page = definition.pages[pageIndex];
            string token = eventKey + "/" + definition.definitionId + "/" + page.pageId;
            foreach (var pending in queue) if (pending.Token == token) return true;
            if (queue.Count >= QueueLimit) return false;
            queue.Add(new Request { Definition = definition, EventKey = eventKey, PageId = page.pageId, Source = source?.Owner,
                SourcePageId = source?.Owner?.ActivePage?.pageId, Token = token, Generation = generation });
            return true;
        }
        bool Valid(Request request, out int pageIndex)
        {
            pageIndex = -1;
            if (request.Generation != generation || request.Definition == null) return false;
            if (request.Source != null && (!request.Source.isActiveAndEnabled || request.Source.ActivePage?.pageId != request.SourcePageId)) return false;
            // Destroyed Unity objects compare equal to null but retain a managed reference.
            if (!ReferenceEquals(request.Source, null) && request.Source == null) return false;
            pageIndex = request.Definition.ResolvePageIndex(session.State, request.EventKey);
            return pageIndex >= 0 && request.Definition.pages[pageIndex].pageId == request.PageId &&
                !request.Definition.IsPageCompleted(session.State, request.EventKey, pageIndex);
        }
        void Dispatch()
        {
            if (!CanAdvance || (Paused && !stepping)) return;
            dispatching = true;
            try
            {
                while (queue.Count > 0)
                {
                    var request = queue[0]; queue.RemoveAt(0);
                    if (registrations.TryGetValue(request.EventKey, out var source)) source.Queued = false;
                    if (!Valid(request, out int pageIndex)) continue;
                    if (session.Events.TryRun(request.Definition, request.EventKey, pageIndex))
                    {
                        if (session.Events.CurrentContext != null) session.Events.CurrentContext.Paused = Paused;
                        return;
                    }
                    if (source != null && request.Definition == source.Owner?.Definition)
                    { source.Faulted = true; source.Owner.MarkAutorunFailed(); }
                }
            }
            finally { dispatching = false; }
        }
        void Pump(int budget)
        {
            if (contexts.Count == 0) return;
            pumping = true;
            try
            {
                int attempts = contexts.Count;
                while (attempts-- > 0 && contexts.Count > 0 && budget > 0 && CanAdvance)
                {
                    cursor %= contexts.Count;
                    var context = contexts[cursor]; cursor++;
                    context.MaxInstructionsPerPump = Math.Min(SliceBudget, budget);
                    context.MoveNext(out object yielded);
                    budget -= Math.Max(1, context.InstructionsLastPump);
                    if (yielded != null && context.Error == null) context.Cancel();
                    if (context.IsComplete || context.IsCancelled) Finish(context);
                }
            }
            finally { pumping = false; }
        }
        public void SetPaused(bool paused)
        {
            Paused = paused;
            if (session?.Events?.CurrentContext != null) session.Events.CurrentContext.Paused = paused;
            foreach (var context in contexts) context.Paused = paused;
        }
        public void Step()
        {
            SetPaused(true); stepping = true;
            try
            {
                if (session?.Events != null && session.Events.IsRunning && session.Events.CurrentContext != null)
                { session.Events.CurrentContext.Step(); return; }
                if (!CanAdvance) return;
                Reconcile();
                if (queue.Count > 0) { Dispatch(); session.Events.CurrentContext?.Step(); return; }
                if (contexts.Count > 0)
                {
                    cursor %= contexts.Count; var context = contexts[cursor];
                    context.Step(); Pump(1);
                }
            }
            finally { stepping = false; }
        }
        // Ordinary transfer keeps the current foreground caller alive until its program returns.
        public void OnMapChanging()
        {
            generation++; queue.Clear();
            foreach (var context in contexts) { context.Cancel(); context.Dispose(); Remember(context); }
            contexts.Clear(); registrations.Clear(); cursor = 0;
        }
        public void CancelAll()
        {
            OnMapChanging(); session?.Events?.Cancel(); Paused = false; activeSeconds = 0;
        }
        internal object CapturePendingRequests() => new List<Request>(queue);
        internal void RestorePendingRequests(object snapshot)
        {
            if (!(snapshot is List<Request> saved) || session?.Map == null) return;
            queue.Clear();
            foreach (var owner in session.Map.EventObjects) if (owner != null) Observe(owner);
            foreach (var request in saved)
            {
                registrations.TryGetValue(request.EventKey, out var source);
                var restored = new Request { Definition = request.Definition, EventKey = request.EventKey, PageId = request.PageId,
                    SourcePageId = request.SourcePageId, Source = source?.Owner, Token = request.Token, Generation = generation };
                if (Valid(restored, out _) && queue.Count < QueueLimit) queue.Add(restored);
            }
        }
        void OnDisable() => CancelAll();
        void OnDestroy() => CancelAll();
    }
}
