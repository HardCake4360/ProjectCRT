using UnityEngine;

namespace NoSleep
{
    public enum TimeMode { AuthoredCosts, Realtime }

    [CreateAssetMenu(fileName = "SurvivalSettings", menuName = "No Sleep/Horror RPG/Survival Settings")]
    public sealed class SurvivalSettings : ScriptableObject
    {
        [Tooltip("Stable ID for this authored rule profile. Save slots currently share the same save directory.")]
        public string profileId = "no_sleep_horror";
        public TimeMode timeMode = TimeMode.AuthoredCosts;
        [Range(0, 23)] public int initialHour = 8;
        [Range(1, 100)] public int campaignDays = 14;
        [Min(0.1f)] public float realSecondsPerGameMinute = 6;
        [Min(0)] public float fatiguePerMinute = 0.02f;
        [Min(0)] public float nightMultiplier = 1.7f;
        [Range(1, 100)] public float criticalFatigue = 85;
        [Range(0.1f, 1)] public float minimumMovementMultiplier = 0.65f;
        [Min(0)] public int healthLossPerDay = 3;
        [Min(0)] public int strengthLossPerDay = 1;
        [Min(1)] public int minMaxHealth = 40;
        [Min(1)] public int minStrength = 4;
        [Range(0, 1)] public float enemyDeclinePerDay = 0.035f;
        [Range(0.01f, 1)] public float minEnemyScale = 0.35f;
    }
}
