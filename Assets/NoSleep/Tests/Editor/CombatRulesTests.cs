using NUnit.Framework;
using UnityEngine;

namespace NoSleep.Tests
{
    public sealed class CombatRulesTests
    {
        GameState state;
        SurvivalSettings settings;
        EnemyDefinition enemy;
        ItemEffectDefinition stimulant;

        [SetUp]
        public void SetUp()
        {
            state = new GameState();
            settings = ScriptableObject.CreateInstance<SurvivalSettings>();
            settings.fatiguePerMinute = 0;
            enemy = ScriptableObject.CreateInstance<EnemyDefinition>();
            enemy.baseHealth = 100;
            stimulant = ScriptableObject.CreateInstance<ItemEffectDefinition>();
            SurvivalRules.Initialize(state, settings);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(settings);
            Object.DestroyImmediate(enemy);
            Object.DestroyImmediate(stimulant);
        }

        [Test]
        public void FatigueReducesDamageWithoutRemovingItsFloorAndSupportIsCapped()
        {
            Assert.That(CombatRules.CalculateStrikeDamage(12, 100), Is.LessThan(CombatRules.CalculateStrikeDamage(12, 0)));
            Assert.That(CombatRules.CalculateStrikeDamage(1, 100), Is.EqualTo(1));
            Assert.That(CombatRules.CalculateStrikeDamage(10, 100), Is.EqualTo(4));
            Assert.That(CombatRules.CalculateStrikeDamage(12, 0, 50), Is.EqualTo(15));
        }

        [Test]
        public void AggressiveRequiresUnlockAndNonlethalCostThenAwakensAndAdvancesTime()
        {
            state.survival.fatigue = 90;
            var rules = new CombatRules(state, enemy, settings);
            Assert.That(rules.Resolve(CombatAction.Aggressive).Accepted, Is.False);
            Assert.That(state.player.health, Is.EqualTo(100));
            state.unlockedActions.Add("aggressive");
            state.player.health = CombatRules.AggressiveHealthCost;
            Assert.That(rules.Resolve(CombatAction.Aggressive).Accepted, Is.False);
            Assert.That(state.player.health, Is.EqualTo(8));
            Assert.That(rules.Turns, Is.Zero);
            state.player.health = 100;
            var turn = rules.Resolve(CombatAction.Aggressive);
            Assert.That(turn.Accepted, Is.True);
            Assert.That(state.player.health, Is.EqualTo(100 - 8 - turn.DamageToPlayer));
            Assert.That(turn.DamageToEnemy, Is.GreaterThan(CombatRules.CalculateStrikeDamage(12, 90)));
            Assert.That(SurvivalRules.EffectiveFatigue(state), Is.EqualTo(55).Within(0.001f));
            Assert.That(state.survival.elapsedMinutes, Is.EqualTo(5));
        }

        [Test]
        public void GuardProtectsOnlyOneRetaliationAndObservationBoostsFollowingStrike()
        {
            var guarded = new CombatRules(state, enemy, settings);
            var guard = guarded.Resolve(CombatAction.Guard);
            var afterGuard = guarded.Resolve(CombatAction.Strike);
            Assert.That(guard.DamageToPlayer, Is.LessThan(afterGuard.DamageToPlayer));
            Assert.That(afterGuard.DamageToPlayer, Is.EqualTo(enemy.baseAttack));

            var observedState = new GameState();
            observedState.unlockedActions.Add("observe");
            SurvivalRules.Initialize(observedState, settings);
            var observed = new CombatRules(observedState, enemy, settings);
            var observe = observed.Resolve(CombatAction.Observe);
            var strike = observed.Resolve(CombatAction.Strike);
            var ordinaryStrike = observed.Resolve(CombatAction.Strike);
            Assert.That(observe.DamageToPlayer, Is.LessThan(strike.DamageToPlayer));
            Assert.That(strike.DamageToEnemy, Is.GreaterThan(ordinaryStrike.DamageToEnemy));
        }

        [Test]
        public void EnemyDeclinesWithElapsedDaysAndNeverDropsBelowOne()
        {
            var firstDay = new CombatRules(state, enemy, settings);
            SurvivalRules.AdvanceTime(state, 7 * 1440, settings);
            var laterDay = new CombatRules(state, enemy, settings);
            Assert.That(laterDay.EnemyMaxHealth, Is.LessThan(firstDay.EnemyMaxHealth));
            Assert.That(laterDay.EnemyAttack, Is.LessThan(firstDay.EnemyAttack));
            Assert.That(CombatRules.ScaledEnemyStat(100, 0.35f), Is.EqualTo(35));
            Assert.That(CombatRules.ScaledEnemyStat(1, 0.01f), Is.EqualTo(1));
        }

        [Test]
        public void StimulantConsumesExactlyOneItemAndAppliesHealthCost()
        {
            state.survival.fatigue = 90;
            state.AddItem(stimulant.itemId, 1);
            var rules = new CombatRules(state, enemy, settings, stimulant);
            var turn = rules.Resolve(CombatAction.UseStimulant);
            Assert.That(turn.Accepted, Is.True);
            Assert.That(state.GetItemCount(stimulant.itemId), Is.Zero);
            Assert.That(state.player.health, Is.EqualTo(100 - stimulant.healthCost - turn.DamageToPlayer));
            Assert.That(SurvivalRules.EffectiveFatigue(state), Is.LessThan(90));
            Assert.That(rules.Resolve(CombatAction.UseStimulant).Accepted, Is.False);
            Assert.That(rules.Turns, Is.EqualTo(1));
        }

        [Test]
        public void FleeGivesNoRewardAndTurnLimitCancelsWithoutWinning()
        {
            var escaped = new CombatRules(state, enemy, settings);
            Assert.That(escaped.Resolve(CombatAction.Flee).Result, Is.EqualTo(CombatResult.Escaped));
            Assert.That(state.player.gold, Is.Zero);
            Assert.That(escaped.Turns, Is.EqualTo(1));
            Assert.That(state.survival.elapsedMinutes, Is.EqualTo(5));

            enemy.retreatAllowed = false;
            enemy.baseAttack = 1;
            state.player.health = 100;
            var longEncounter = new CombatRules(state, enemy, settings);
            Assert.That(longEncounter.Resolve(CombatAction.Flee).Accepted, Is.False);
            for (int turn = 0; turn < CombatRules.MaximumTurns; turn++) longEncounter.Resolve(CombatAction.Guard);
            Assert.That(longEncounter.Result, Is.EqualTo(CombatResult.Cancelled));
            Assert.That(longEncounter.EnemyHealth, Is.GreaterThan(0));
            Assert.That(state.player.gold, Is.Zero);
        }

        [Test]
        public void SavedActionIdsTrimAndDeduplicateBeforeUnlockedConditionsEvaluate()
        {
            state.unlockedActions = new System.Collections.Generic.List<string>
                { " strike ", "aggressive", " aggressive ", " observe ", null, "" };
            var restored = SaveService.Deserialize(SaveService.Serialize(new SaveData
                { mapId = "test_actions", position = Vector3.zero, state = state })).state;
            CollectionAssert.AreEqual(new[] { "strike", "aggressive", "observe" }, restored.unlockedActions);
            Assert.That(new StateCondition { kind = ConditionKind.ActionUnlocked, key = " aggressive " }
                .Evaluate(restored, "test/event"), Is.True);
        }
    }
}
