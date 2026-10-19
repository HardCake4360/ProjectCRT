using System;
using System.Collections.Generic;
using UnityEngine;

namespace NoSleep.Editor
{
    // Authoring declarations only. No runtime state or save format depends on this asset.
    [CreateAssetMenu(menuName = "No Sleep/State Key Registry", fileName = "StateKeyRegistry")]
    public sealed class StateKeyRegistry : ScriptableObject
    {
        public List<string> switchKeys = new List<string>();
        public List<string> variableKeys = new List<string>();
        public List<string> itemIds = new List<string>();
        public List<MapLinkPolicy> mapLinks = new List<MapLinkPolicy>();
    }

    [Serializable]
    public sealed class MapLinkPolicy
    {
        public MapDefinition source;
        public MapDefinition destination;
        [Tooltip("This directed connection is intentionally one way.")]
        public bool allowOneWay;
        [Tooltip("Report an error if no authored transfer connects source to destination.")]
        public bool requireConnection;
    }
}
