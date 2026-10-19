using System;
using System.Collections.Generic;

namespace NoSleep
{
    [Serializable]
    public sealed class PartyState
    {
        public List<string> knownCompanions = new List<string>();
        public List<string> activeCompanions = new List<string>();

        public void Normalize()
        {
            knownCompanions = CleanIds(knownCompanions);
            activeCompanions = CleanIds(activeCompanions);
            foreach (var id in activeCompanions)
                if (!knownCompanions.Contains(id)) knownCompanions.Add(id);
        }

        internal static List<string> CleanIds(List<string> values)
        {
            var result = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            if (values != null)
                foreach (var value in values)
                    if (!string.IsNullOrWhiteSpace(value) && seen.Add(value.Trim())) result.Add(value.Trim());
            return result;
        }
    }
}
