using UnityEngine;

namespace NoSleep
{
    [CreateAssetMenu(fileName = "Stimulant", menuName = "No Sleep/Horror RPG/Item Effect")]
    public sealed class ItemEffectDefinition : ScriptableObject
    {
        public string itemId = "stimulant";
        public string displayName = "각성제";
        [Min(0)] public int healthCost = 6;
        [Min(0)] public int sanityCost;
        [Range(0, 100)] public float awakeningRelief = 35;
        [Min(0)] public int durationMinutes = 120;
    }
}
