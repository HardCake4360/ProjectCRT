using System;
using UnityEngine;

namespace NoSleep
{
    [Serializable]
    public sealed class SurvivalState
    {
        public int elapsedMinutes;
        public int startHour = 8;
        public float fatigue;
        public int awakenedUntilMinutes;
        public float awakeningRelief;
        public int baselineMaxHealth = 100;
        public int baselineStrength = 12;
        public bool initialized;

        public void Normalize()
        {
            elapsedMinutes = Math.Max(0, elapsedMinutes);
            startHour = Mathf.Clamp(startHour, 0, 23);
            awakenedUntilMinutes = Math.Max(0, awakenedUntilMinutes);
            fatigue = FinitePercent(fatigue);
            awakeningRelief = FinitePercent(awakeningRelief);
            baselineMaxHealth = Math.Max(1, baselineMaxHealth);
            baselineStrength = Math.Max(1, baselineStrength);
            if (awakenedUntilMinutes <= elapsedMinutes) awakeningRelief = 0;
        }

        internal static float FinitePercent(float value)
        {
            if (float.IsNaN(value)) return 0;
            return Mathf.Clamp(value, 0, 100);
        }
    }
}
