using System;
using UnityEngine;

namespace NoSleep
{
    /// <summary>Deterministic prototype rules; authored data and serializable state remain separate.</summary>
    public static class SurvivalRules
    {
        const int MinutesPerDay = 1440;
        const int NightStart = 20 * 60;
        const int DayStart = 6 * 60;

        public static void Initialize(GameState state, SurvivalSettings settings)
        {
            RequireState(state);
            if (InitializeCore(state, settings)) state.NotifyChanged();
        }

        public static int Day(GameState state, SurvivalSettings settings = null)
            => state == null ? 1 : (int)(AbsoluteMinutes(state, settings) / MinutesPerDay) + 1;

        public static int Hour(GameState state, SurvivalSettings settings = null)
            => state == null ? InitialHour(settings) : (int)(AbsoluteMinutes(state, settings) % MinutesPerDay) / 60;

        public static bool IsNight(GameState state, SurvivalSettings settings = null)
        {
            int hour = Hour(state, settings);
            return hour >= 20 || hour < 6;
        }

        public static float EffectiveFatigue(GameState state, SurvivalSettings settings = null)
        {
            if (state?.survival == null) return 0;
            var survival = state.survival;
            float relief = survival.awakenedUntilMinutes > survival.elapsedMinutes
                ? SurvivalState.FinitePercent(survival.awakeningRelief) : 0;
            return Mathf.Max(0, SurvivalState.FinitePercent(survival.fatigue) - relief);
        }

        public static float MovementMultiplier(GameState state, SurvivalSettings settings)
        {
            float minimum = Setting(settings != null ? settings.minimumMovementMultiplier : 0.65f, 0.65f, 0.1f, 1);
            float critical = Setting(settings != null ? settings.criticalFatigue : 85, 85, 1, 100);
            return Mathf.Lerp(1, minimum, Mathf.Clamp01(EffectiveFatigue(state) / critical));
        }

        public static void AdvanceTime(GameState state, int minutes, SurvivalSettings settings, float fatigueMultiplier = 1)
        {
            RequireState(state);
            if (minutes <= 0) return;
            InitializeCore(state, settings);
            AdvanceCore(state, minutes, settings, fatigueMultiplier);
            state.NotifyChanged();
        }

        public static void Awaken(GameState state, float relief, int durationMinutes)
        {
            RequireState(state);
            state.Normalize();
            ApplyAwakening(state.survival, relief, durationMinutes);
            state.NotifyChanged();
        }

        public static bool UseItem(GameState state, ItemEffectDefinition effect, SurvivalSettings settings)
        {
            RequireState(state);
            if (effect == null || string.IsNullOrWhiteSpace(effect.itemId)) return false;
            state.Normalize();
            string id = effect.itemId.Trim();
            var entry = state.inventory.Find(value => value != null && value.key?.Trim() == id && value.value > 0);
            int healthCost = Math.Max(0, effect.healthCost), sanityCost = Math.Max(0, effect.sanityCost);
            int availableHealth = state.player.health;
            if (!state.survival.initialized)
                availableHealth = Math.Min(availableHealth, DeclinedStat(state.player.maxHealth,
                    settings != null ? settings.minMaxHealth : 40,
                    settings != null ? settings.healthLossPerDay : 3, DeclineDays(state, settings)));
            if (entry == null || availableHealth <= healthCost) return false;
            InitializeCore(state, settings);
            entry.value--;
            state.player.health -= healthCost;
            state.player.sanity = Math.Max(0, state.player.sanity - sanityCost);
            ApplyAwakening(state.survival, effect.awakeningRelief, effect.durationMinutes);
            state.NotifyChanged();
            return true;
        }

        public static float EnemyScale(GameState state, SurvivalSettings settings)
        {
            float decline = Setting(settings != null ? settings.enemyDeclinePerDay : 0.035f, 0.035f, 0, 1);
            float minimum = Setting(settings != null ? settings.minEnemyScale : 0.35f, 0.35f, 0.01f, 1);
            return Mathf.Clamp(1 - DeclineDays(state, settings) * decline, minimum, 1);
        }

        public static void JoinCompanion(GameState state, string companionId)
        {
            RequireState(state); string id = Id(companionId); state.Normalize();
            bool changed = false;
            if (!state.party.knownCompanions.Contains(id)) { state.party.knownCompanions.Add(id); changed = true; }
            if (!state.party.activeCompanions.Contains(id)) { state.party.activeCompanions.Add(id); changed = true; }
            if (changed) state.NotifyChanged();
        }

        public static void LeaveCompanion(GameState state, string companionId)
        {
            RequireState(state); string id = Id(companionId); state.Normalize();
            if (state.party.activeCompanions.Remove(id)) state.NotifyChanged();
        }

        public static void ClearParty(GameState state)
        {
            RequireState(state); state.Normalize();
            if (state.party.activeCompanions.Count == 0) return;
            state.party.activeCompanions.Clear(); state.NotifyChanged();
        }

        public static bool RecordClue(GameState state, string clueId, string title, string text)
        {
            RequireState(state); string id = Id(clueId); state.Normalize();
            if (state.journey.clueIds.Contains(id)) return false;
            state.journey.clueIds.Add(id);
            state.journey.journal.Add(new JournalRecord { id = "clue:" + id, title = title ?? id, text = text ?? string.Empty });
            state.NotifyChanged(); return true;
        }

        public static void SetObjective(GameState state, string objectiveId, string title, int stage = 0)
        {
            RequireState(state); string id = Id(objectiveId); state.Normalize();
            state.journey.objectiveId = id; state.journey.objectiveTitle = title ?? id;
            state.journey.stage = Math.Max(0, stage); state.NotifyChanged();
        }

        public static void UnlockAction(GameState state, string actionId)
        {
            RequireState(state); string id = Id(actionId); state.Normalize();
            if (state.unlockedActions.Contains(id)) return;
            state.unlockedActions.Add(id); state.NotifyChanged();
        }

        public static bool CompleteNightActivity(GameState state, NightActivityDefinition activity, SurvivalSettings settings)
        {
            RequireState(state);
            if (activity == null || string.IsNullOrWhiteSpace(activity.activityId)) return false;
            state.Normalize();
            if (activity.requiresCompanion && state.party.activeCompanions.Count == 0) return false;
            int currentMinute = (int)(AbsoluteMinutes(state, settings) % MinutesPerDay);
            int beforeNight = currentMinute >= DayStart && currentMinute < NightStart ? NightStart - currentMinute : 0;
            int dawn = Mathf.Clamp(activity.dawnHour, 0, 23) * 60;
            currentMinute = (currentMinute + beforeNight) % MinutesPerDay;
            int untilDawn = (dawn - currentMinute + MinutesPerDay) % MinutesPerDay;
            if (untilDawn == 0) untilDawn = MinutesPerDay;
            // Refuse an incomplete night rather than granting repeatable recovery at a saturated clock.
            if ((long)beforeNight + untilDawn > (long)int.MaxValue - state.survival.elapsedMinutes) return false;
            InitializeCore(state, settings);
            if (beforeNight > 0) AdvanceCore(state, beforeNight, settings, 1);
            AdvanceCore(state, untilDawn, settings, activity.fatigueMultiplier);
            state.player.health = Restore(state.player.health, activity.healthRestore, state.player.maxHealth);
            state.player.sanity = Restore(state.player.sanity, activity.sanityRestore, state.player.maxSanity);
            string id = activity.activityId.Trim();
            if (!state.journey.memories.Exists(memory => memory.id == id))
                state.journey.memories.Add(new MemoryRecord { id = id, title = activity.memoryTitle ?? activity.displayName ?? id, text = activity.memoryText ?? string.Empty });
            string journalId = "night:" + id + ":" + Day(state, settings);
            if (!state.journey.journal.Exists(record => record.id == journalId))
                state.journey.journal.Add(new JournalRecord { id = journalId, title = activity.displayName ?? id, text = activity.description ?? string.Empty });
            state.NotifyChanged(); return true;
        }

        static bool InitializeCore(GameState state, SurvivalSettings settings)
        {
            state.Normalize();
            if (state.survival.initialized) return false;
            state.survival.baselineMaxHealth = state.player.maxHealth;
            state.survival.baselineStrength = Math.Max(1, state.player.strength);
            state.survival.startHour = InitialHour(settings);
            state.survival.initialized = true;
            ApplyDecline(state, settings);
            return true;
        }

        static void AdvanceCore(GameState state, int minutes, SurvivalSettings settings, float fatigueMultiplier)
        {
            int actualMinutes = (int)Math.Min(Math.Max(0L, minutes), (long)int.MaxValue - state.survival.elapsedMinutes);
            long start = AbsoluteMinutes(state, settings);
            long nightMinutes = NightMinutesBefore(start + actualMinutes) - NightMinutesBefore(start);
            float rate = Setting(settings != null ? settings.fatiguePerMinute : 0.02f, 0.02f, 0, 100);
            float nightMultiplier = Setting(settings != null ? settings.nightMultiplier : 1.7f, 1.7f, 0, 100);
            float multiplier = Setting(fatigueMultiplier, 1, 0, 100);
            double gained = ((actualMinutes - nightMinutes) + nightMinutes * (double)nightMultiplier) * rate * multiplier;
            state.survival.fatigue = (float)Math.Min(100, Math.Max(0, state.survival.fatigue + gained));
            state.survival.elapsedMinutes += actualMinutes;
            if (state.survival.awakenedUntilMinutes <= state.survival.elapsedMinutes) state.survival.awakeningRelief = 0;
            ApplyDecline(state, settings);
        }

        // Counts the two night ranges over whole calendar days in O(1), preserving split-step equivalence.
        static long NightMinutesBefore(long absoluteMinute)
        {
            long days = absoluteMinute / MinutesPerDay, remainder = absoluteMinute % MinutesPerDay;
            return days * 600 + Math.Min(remainder, DayStart) + Math.Max(0, remainder - NightStart);
        }

        static void ApplyDecline(GameState state, SurvivalSettings settings)
        {
            int days = DeclineDays(state, settings);
            state.player.maxHealth = DeclinedStat(state.survival.baselineMaxHealth,
                settings != null ? settings.minMaxHealth : 40, settings != null ? settings.healthLossPerDay : 3, days);
            state.player.strength = DeclinedStat(state.survival.baselineStrength,
                settings != null ? settings.minStrength : 4, settings != null ? settings.strengthLossPerDay : 1, days);
            state.player.health = Mathf.Clamp(state.player.health, 0, state.player.maxHealth);
        }

        static void ApplyAwakening(SurvivalState survival, float relief, int durationMinutes)
        {
            if (durationMinutes <= 0 || SurvivalState.FinitePercent(relief) <= 0) return;
            int expiry = (int)Math.Min(int.MaxValue, (long)survival.elapsedMinutes + durationMinutes);
            if (expiry <= survival.elapsedMinutes) return;
            survival.awakeningRelief = Mathf.Max(survival.awakeningRelief, SurvivalState.FinitePercent(relief));
            survival.awakenedUntilMinutes = Math.Max(survival.awakenedUntilMinutes, expiry);
        }

        static int DeclineDays(GameState state, SurvivalSettings settings)
            => Mathf.Clamp(Day(state, settings) - 1, 0, Mathf.Clamp(settings != null ? settings.campaignDays : 14, 1, 100) - 1);
        static int DeclinedStat(int baseline, int minimum, int lossPerDay, int days)
            => (int)Math.Max(Math.Min(baseline, Math.Max(1, minimum)), (long)baseline - (long)days * Math.Max(0, lossPerDay));
        static int InitialHour(SurvivalSettings settings) => Mathf.Clamp(settings != null ? settings.initialHour : 8, 0, 23);
        static long AbsoluteMinutes(GameState state, SurvivalSettings settings)
            => (long)(state.survival != null && (state.survival.initialized || settings == null)
                ? Mathf.Clamp(state.survival.startHour, 0, 23) : InitialHour(settings)) * 60
                + Math.Max(0, state.survival?.elapsedMinutes ?? 0);
        static float Setting(float value, float fallback, float minimum, float maximum)
            => float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp(value, minimum, maximum);
        static int Restore(int value, int amount, int maximum)
            => (int)Math.Min(maximum, (long)value + Math.Max(0, amount));
        static string Id(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("ID cannot be empty.", nameof(id));
            return id.Trim();
        }
        static void RequireState(GameState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
        }
    }
}
