using System;

namespace NoSleep
{
    public enum ConditionKind { Always, Switch, Variable, SelfSwitch, Item, Day, EffectiveFatigue, Companion, Clue, ActionUnlocked }
    public enum NumberComparison { Equal, NotEqual, Greater, GreaterOrEqual, Less, LessOrEqual }

    [Serializable]
    public sealed class StateCondition
    {
        public ConditionKind kind;
        public string key;
        public bool expected = true;
        public NumberComparison comparison = NumberComparison.GreaterOrEqual;
        public int value = 1;

        public bool Evaluate(GameState state, string eventKey)
        {
            if (kind == ConditionKind.Always) return true;
            if (state == null) return false;
            if (kind == ConditionKind.Day)
                return Compare(SurvivalRules.Day(state), value, comparison);
            if (kind == ConditionKind.EffectiveFatigue) return Compare((int)System.Math.Floor(SurvivalRules.EffectiveFatigue(state)), value, comparison);
            if (string.IsNullOrWhiteSpace(key)) return false;
            switch (kind)
            {
                case ConditionKind.Switch: return state.GetSwitch(key) == expected;
                case ConditionKind.SelfSwitch: return !string.IsNullOrWhiteSpace(eventKey) && state.GetSelfSwitch(eventKey, key) == expected;
                case ConditionKind.Variable: return Compare(state.GetVariable(key), value, comparison);
                case ConditionKind.Item: return Compare(state.GetItemCount(key), value, comparison);
                case ConditionKind.Companion: return (state.party?.activeCompanions?.Contains(key.Trim()) == true) == expected;
                case ConditionKind.Clue: return (state.journey?.clueIds?.Contains(key.Trim()) == true) == expected;
                case ConditionKind.ActionUnlocked: return (state.unlockedActions?.Contains(key.Trim()) == true) == expected;
                default: return false;
            }
        }
        public static bool Compare(int left, int right, NumberComparison comparison)
        {
            switch (comparison)
            {
                case NumberComparison.Equal: return left == right;
                case NumberComparison.NotEqual: return left != right;
                case NumberComparison.Greater: return left > right;
                case NumberComparison.GreaterOrEqual: return left >= right;
                case NumberComparison.Less: return left < right;
                case NumberComparison.LessOrEqual: return left <= right;
                default: return false;
            }
        }
    }
}
