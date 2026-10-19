using UnityEngine;

namespace NoSleep
{
    [CreateAssetMenu(fileName = "NightActivity", menuName = "No Sleep/Horror RPG/Night Activity")]
    public sealed class NightActivityDefinition : ScriptableObject
    {
        public string activityId = "band_night";
        public string displayName = "엉망진창 밤샘 연주";
        [TextArea(2, 6)] public string description;
        [Range(0, 23)] public int dawnHour = 6;
        [Min(0)] public float fatigueMultiplier = 0.2f;
        [Min(0)] public int healthRestore = 8;
        [Min(0)] public int sanityRestore = 15;
        public bool requiresCompanion = true;
        public string memoryTitle = "함께 버틴 밤";
        [TextArea(2, 6)] public string memoryText;
    }
}
