using System;

namespace NoSleep
{
    [Serializable]
    public sealed class EventCheckpointState
    {
        public string scope;
        public string checkpointId;
        // A deadline in the same saved elapsed-minute clock as SurvivalState.
        public int dueElapsedMinutes;
    }

    public enum StateMutationKind { SetSwitch, SetVariable, SetSelfSwitch, AddVariable, AddItem, AddGold }

    [Serializable]
    public sealed class StateMutation
    {
        public StateMutationKind kind;
        public string key;
        public int intValue;
        public bool boolValue = true;

        internal void Apply(GameState state, string eventKey)
        {
            switch (kind)
            {
                case StateMutationKind.SetSwitch: state.SetSwitch(key, boolValue); break;
                case StateMutationKind.SetVariable: state.SetVariable(key, intValue); break;
                case StateMutationKind.SetSelfSwitch:
                    if ((key ?? "").Trim().StartsWith("__nosleep_", StringComparison.Ordinal))
                        throw new ArgumentException("Transaction effects cannot write reserved event progress keys.", nameof(key));
                    state.SetSelfSwitch(eventKey, key, boolValue); break;
                case StateMutationKind.AddVariable: state.AddVariable(key, intValue); break;
                case StateMutationKind.AddItem: state.AddItem(key, intValue); break;
                case StateMutationKind.AddGold: state.AddGold(intValue); break;
                default: throw new ArgumentOutOfRangeException(nameof(kind), "Unknown transaction effect.");
            }
        }
    }
}
