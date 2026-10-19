using System;
using System.Collections.Generic;
using UnityEngine;

namespace NoSleep
{
    public readonly struct CombatTurnResult
    {
        public bool Accepted { get; }
        public int DamageToPlayer { get; }
        public int DamageToEnemy { get; }
        public string Message { get; }
        public CombatResult? Result { get; }

        public CombatTurnResult(bool accepted, int playerDamage, int enemyDamage, string message, CombatResult? result)
        {
            Accepted = accepted;
            DamageToPlayer = playerDamage;
            DamageToEnemy = enemyDamage;
            Message = message;
            Result = result;
        }
    }

    // An encounter contains no random rolls. Progression state owns all lasting costs;
    // only the enemy's current health and observation bonus live for this encounter.
    public sealed class CombatRules
    {
        public const int MaximumTurns = 50;
        public const int MinutesPerTurn = 5;
        public const int AggressiveHealthCost = 8;
        readonly GameState state;
        readonly EnemyDefinition enemy;
        readonly SurvivalSettings settings;
        readonly ItemEffectDefinition stimulant;
        bool observed;

        public int EnemyHealth { get; private set; }
        public int EnemyMaxHealth { get; private set; }
        public int EnemyAttack { get; private set; }
        public int Turns { get; private set; }
        public CombatResult? Result { get; private set; }
        public bool IsFinished => Result.HasValue;

        public CombatRules(GameState state, EnemyDefinition enemy, SurvivalSettings settings, ItemEffectDefinition stimulant = null)
        {
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            this.enemy = enemy != null ? enemy : throw new ArgumentNullException(nameof(enemy));
            this.settings = settings != null ? settings : throw new ArgumentNullException(nameof(settings));
            this.stimulant = stimulant;
            if (state.player == null) throw new ArgumentException("Combat needs player statistics.", nameof(state));
            SurvivalRules.Initialize(state, settings);
            RefreshEnemyScale();
            EnemyHealth = EnemyMaxHealth;
            if (state.player.health <= 0) Result = CombatResult.Defeat;
        }

        public List<CombatAction> GetAvailableActions()
        {
            var actions = new List<CombatAction>();
            if (IsFinished) return actions;
            actions.Add(CombatAction.Strike);
            actions.Add(CombatAction.Guard);
            if (enemy.retreatAllowed) actions.Add(CombatAction.Flee);
            if (Unlocked("aggressive") && state.player.health > AggressiveHealthCost) actions.Add(CombatAction.Aggressive);
            if (Unlocked("observe")) actions.Add(CombatAction.Observe);
            if (CanUseStimulant()) actions.Add(CombatAction.UseStimulant);
            return actions;
        }

        public CombatTurnResult Resolve(CombatAction action)
        {
            if (IsFinished) return Rejected("이미 종료된 전투입니다.");
            if (!GetAvailableActions().Contains(action)) return Rejected("지금은 그 행동을 사용할 수 없습니다.");

            int damageToEnemy = 0;
            int damageToPlayer = 0;
            float retaliationMultiplier = 1f;
            string message;
            switch (action)
            {
                case CombatAction.Flee:
                    message = "거리를 벌려 접촉에서 벗어났다.";
                    FinishTurn();
                    Result = state.player.health <= 0 ? CombatResult.Defeat : CombatResult.Escaped;
                    return new CombatTurnResult(true, 0, 0, message, Result);
                case CombatAction.Guard:
                    retaliationMultiplier = 0.35f;
                    message = "몸을 보호하며 다음 공격을 버텼다.";
                    break;
                case CombatAction.Observe:
                    observed = true;
                    retaliationMultiplier = 0.55f;
                    message = "움직임을 관찰했다. 다음 공격이 강해진다.";
                    break;
                case CombatAction.UseStimulant:
                    if (!SurvivalRules.UseItem(state, stimulant, settings)) return Rejected("아이템을 사용할 수 없습니다.");
                    message = stimulant.displayName + "를 사용했다.";
                    break;
                case CombatAction.Aggressive:
                    state.player.health -= AggressiveHealthCost;
                    SurvivalRules.Awaken(state, 35f, 90);
                    damageToEnemy = CalculateStrikeDamage(state.player.strength, SurvivalRules.EffectiveFatigue(state),
                        CompanionSupport(), (observed ? 1.35f : 1f) * 1.8f);
                    observed = false;
                    message = $"체력 {AggressiveHealthCost}을 소모해 과격하게 공격했다. 피해 {damageToEnemy}.";
                    break;
                case CombatAction.Strike:
                    damageToEnemy = CalculateStrikeDamage(state.player.strength, SurvivalRules.EffectiveFatigue(state),
                        CompanionSupport(), observed ? 1.35f : 1f);
                    observed = false;
                    message = $"공격했다. 피해 {damageToEnemy}.";
                    break;
                default: return Rejected("알 수 없는 전투 행동입니다.");
            }

            EnemyHealth = Math.Max(0, EnemyHealth - damageToEnemy);
            if (EnemyHealth > 0)
            {
                damageToPlayer = ReducedAttack(EnemyAttack, retaliationMultiplier);
                state.player.health = Math.Max(0, state.player.health - damageToPlayer);
                message += $" 상대의 공격으로 체력 {damageToPlayer}을 잃었다.";
            }
            FinishTurn();
            if (state.player.health <= 0) Result = CombatResult.Defeat;
            else if (EnemyHealth <= 0) Result = CombatResult.Victory;
            else if (Turns >= MaximumTurns) Result = CombatResult.Cancelled;
            return new CombatTurnResult(true, damageToPlayer, damageToEnemy, message, Result);
        }

        public void Cancel() { if (!IsFinished) Result = CombatResult.Cancelled; }

        public static int CalculateStrikeDamage(int strength, float fatigue, int companions = 0, float multiplier = 1f)
        {
            double fatigueFactor = 1d - (float.IsNaN(fatigue) ? 100d : Math.Max(0d, Math.Min(100d, fatigue))) * 0.006d;
            int support = Mathf.Clamp(companions, 0, 3);
            double safeMultiplier = float.IsNaN(multiplier) || float.IsInfinity(multiplier) ? 1d : Math.Max(0d, multiplier);
            double damage = Math.Max(1, strength) * fatigueFactor * safeMultiplier + support;
            return (int)Math.Max(1d, Math.Min(int.MaxValue, Math.Floor(damage)));
        }

        public static int ScaledEnemyStat(int baseValue, float scale) =>
            (int)Math.Max(1d, Math.Min(int.MaxValue, Math.Floor(Math.Max(1, baseValue) *
                (double)(float.IsNaN(scale) ? 1f : Mathf.Clamp01(scale)) + 0.00001d)));

        static int ReducedAttack(int attack, float multiplier) =>
            (int)Math.Max(1d, Math.Min(int.MaxValue, Math.Floor(attack * (double)multiplier)));
        bool Unlocked(string key) => state.unlockedActions != null && state.unlockedActions.Contains(key);
        bool CanUseStimulant() => stimulant != null && !string.IsNullOrWhiteSpace(stimulant.itemId) &&
            state.GetItemCount(stimulant.itemId) > 0 && state.player.health > Math.Max(0, stimulant.healthCost);
        int CompanionSupport() => state.party?.activeCompanions?.Count ?? 0;

        void FinishTurn()
        {
            Turns++;
            SurvivalRules.AdvanceTime(state, MinutesPerTurn, settings);
            RefreshEnemyScale();
            EnemyHealth = Math.Min(EnemyHealth, EnemyMaxHealth);
            state.NotifyChanged();
        }

        void RefreshEnemyScale()
        {
            float scale = SurvivalRules.EnemyScale(state, settings);
            EnemyMaxHealth = ScaledEnemyStat(enemy.baseHealth, scale);
            EnemyAttack = ScaledEnemyStat(enemy.baseAttack, scale);
        }

        CombatTurnResult Rejected(string message) => new CombatTurnResult(false, 0, 0, message, Result);
    }
}
