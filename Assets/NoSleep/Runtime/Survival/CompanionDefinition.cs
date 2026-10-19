using UnityEngine;

namespace NoSleep
{
    [CreateAssetMenu(fileName = "Companion", menuName = "No Sleep/Horror RPG/Companion")]
    public sealed class CompanionDefinition : ScriptableObject
    {
        public string companionId = "companion";
        public string displayName = "동료";
        [TextArea(2, 6)] public string description;
        public Color color = new Color(0.45f, 0.7f, 0.65f);
    }
}
