using System.Collections.Generic;
using UnityEngine;

namespace NoSleep
{
    // Followers are presentation only. PartyState remains the authoritative saved roster.
    public sealed class PartyFollowers : MonoBehaviour
    {
        GameSession session;
        Material material;
        readonly List<string> ids = new List<string>();
        readonly List<GameObject> visuals = new List<GameObject>();
        public void Configure(GameSession game, Material playerMaterial) { session = game; material = playerMaterial; }
        void LateUpdate()
        {
            if (session == null || !session.IsReady) return;
            var party = session.State.party.activeCompanions;
            bool changed = party.Count != ids.Count;
            if (!changed) for (int i = 0; i < ids.Count; i++) if (ids[i] != party[i]) { changed = true; break; }
            if (changed) Rebuild(party);
            for (int i = 0; i < visuals.Count; i++)
            {
                var visual = visuals[i].transform;
                var target = transform.position - transform.forward * (1.1f + i * .8f) + Vector3.up * .85f;
                if (!session.Map.CanOccupy(target - Vector3.up * .85f, .18f)) target = transform.position + Vector3.up * .85f;
                visual.position = Vector3.Distance(visual.position, target) > 5 ? target : Vector3.Lerp(visual.position, target, 1 - Mathf.Exp(-8 * Time.deltaTime));
                visual.rotation = transform.rotation;
            }
        }
        void Rebuild(List<string> party)
        {
            foreach (var visual in visuals) if (visual != null) { visual.SetActive(false); Destroy(visual); }
            ids.Clear(); visuals.Clear();
            foreach (string id in party)
            {
                ids.Add(id); var definition = session.FindCompanion(id);
                var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule); visual.name = session.CompanionName(id);
                visual.transform.SetParent(session.transform, true); visual.transform.position = transform.position + Vector3.up * .85f;
                visual.transform.localScale = new Vector3(.4f, .8f, .4f);
                var collider = visual.GetComponent<Collider>(); collider.enabled = false; Destroy(collider);
                var renderer = visual.GetComponent<Renderer>(); if (material != null) renderer.sharedMaterial = material;
                var properties = new MaterialPropertyBlock(); var color = definition != null ? definition.color : Color.cyan;
                properties.SetColor("_BaseColor", color); properties.SetColor("_Color", color); renderer.SetPropertyBlock(properties);
                visuals.Add(visual);
            }
        }
        void OnDestroy() { foreach (var visual in visuals) if (visual != null) Destroy(visual); }
    }
}
