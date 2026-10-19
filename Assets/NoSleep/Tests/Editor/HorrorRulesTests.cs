using System;
using NUnit.Framework;
using UnityEngine;

namespace NoSleep.Tests
{
    public sealed class HorrorRulesTests
    {
        SurvivalSettings settings;
        ItemEffectDefinition item;
        NightActivityDefinition activity;

        [SetUp]
        public void SetUp()
        {
            settings = ScriptableObject.CreateInstance<SurvivalSettings>();
            item = ScriptableObject.CreateInstance<ItemEffectDefinition>();
            activity = ScriptableObject.CreateInstance<NightActivityDefinition>();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(settings);
            UnityEngine.Object.DestroyImmediate(item);
            UnityEngine.Object.DestroyImmediate(activity);
        }

        [Test]
        public void TimeSpanningDayAndNightMatchesSplitAdvances()
        {
            settings.initialHour = 18; settings.fatiguePerMinute = 0.01f; settings.nightMultiplier = 2;
            var whole = new GameState(); var split = new GameState();
            SurvivalRules.AdvanceTime(whole, 840, settings);
            SurvivalRules.AdvanceTime(split, 120, settings);
            SurvivalRules.AdvanceTime(split, 600, settings);
            SurvivalRules.AdvanceTime(split, 120, settings);
            Assert.That(whole.survival.fatigue, Is.EqualTo(14.4f).Within(0.0001f));
            Assert.That(split.survival.fatigue, Is.EqualTo(whole.survival.fatigue).Within(0.0001f));
            Assert.That(SurvivalRules.Day(whole, settings), Is.EqualTo(2));
            Assert.That(SurvivalRules.Hour(whole, settings), Is.EqualTo(8));
            Assert.That(SurvivalRules.IsNight(whole, settings), Is.False);
            Assert.That(split.player.maxHealth, Is.EqualTo(whole.player.maxHealth));
        }

        [TestCase(5, true)]
        [TestCase(6, false)]
        [TestCase(19, false)]
        [TestCase(20, true)]
        public void NightBoundaryUsesSavedStartingHour(int hour, bool night)
        {
            settings.initialHour = hour;
            var state = new GameState(); SurvivalRules.Initialize(state, settings);
            settings.initialHour = 12;
            Assert.That(SurvivalRules.Hour(state, settings), Is.EqualTo(hour));
            Assert.That(SurvivalRules.IsNight(state, settings), Is.EqualTo(night));
        }

        [Test]
        public void SavedBaselineAndCalendarDoNotResetWhenInitializingAgain()
        {
            var state = new GameState { player = new PlayerStats { maxHealth = 140, health = 120, strength = 16 } };
            SurvivalRules.Initialize(state, settings);
            SurvivalRules.AdvanceTime(state, 960, settings);
            settings.initialHour = 23;
            SurvivalRules.Initialize(state, settings);
            Assert.That(state.survival.startHour, Is.EqualTo(8));
            Assert.That(state.survival.baselineMaxHealth, Is.EqualTo(140));
            Assert.That(state.survival.baselineStrength, Is.EqualTo(16));
            Assert.That(state.player.maxHealth, Is.EqualTo(137));
            Assert.That(state.player.strength, Is.EqualTo(15));
            Assert.That(SurvivalRules.Day(state, settings), Is.EqualTo(2));
            Assert.That(SurvivalRules.Hour(state, settings), Is.Zero);
        }

        [Test]
        public void AwakeningExpiresAtExactMinuteWithoutDeletingUnderlyingFatigue()
        {
            var state = new GameState(); SurvivalRules.Initialize(state, settings);
            state.survival.fatigue = 60;
            SurvivalRules.Awaken(state, 35, 120);
            Assert.That(SurvivalRules.EffectiveFatigue(state), Is.EqualTo(25));
            Assert.That(state.survival.fatigue, Is.EqualTo(60));
            SurvivalRules.AdvanceTime(state, 119, settings);
            Assert.That(SurvivalRules.EffectiveFatigue(state), Is.EqualTo(27.38f).Within(0.0001f));
            SurvivalRules.AdvanceTime(state, 1, settings);
            Assert.That(SurvivalRules.EffectiveFatigue(state), Is.EqualTo(62.4f).Within(0.0001f));
            Assert.That(state.survival.awakeningRelief, Is.Zero);
        }

        [Test]
        public void ItemConsumesOnceAppliesCostsAndEmitsOneChange()
        {
            var state = new GameState(); SurvivalRules.Initialize(state, settings);
            state.AddItem("stimulant", 1); state.player.health = 7; state.survival.fatigue = 60;
            state.player.sanity = 2; item.sanityCost = 5;
            int changes = 0; state.Changed += () => changes++;
            Assert.That(SurvivalRules.UseItem(state, item, settings), Is.True);
            Assert.That(state.GetItemCount("stimulant"), Is.Zero);
            Assert.That(state.player.health, Is.EqualTo(1));
            Assert.That(state.player.sanity, Is.Zero);
            Assert.That(SurvivalRules.EffectiveFatigue(state), Is.EqualTo(25));
            Assert.That(changes, Is.EqualTo(1));
            Assert.That(SurvivalRules.UseItem(state, item, settings), Is.False);
            Assert.That(changes, Is.EqualTo(1));
        }

        [Test]
        public void ItemCannotSpendFinalHealthAndFailedUsePreservesInventory()
        {
            var state = new GameState(); state.AddItem("stimulant", 2); state.player.health = 6;
            Assert.That(SurvivalRules.UseItem(state, item, settings), Is.False);
            Assert.That(state.GetItemCount("stimulant"), Is.EqualTo(2));
            Assert.That(state.player.health, Is.EqualTo(6));
            Assert.That(state.survival.awakeningRelief, Is.Zero);
        }

        [Test]
        public void ItemCannotBypassDeclineBeforeFirstInitialization()
        {
            var state = new GameState(); state.AddItem("stimulant", 1);
            state.survival.elapsedMinutes = 13 * 1440; item.healthCost = 61;
            Assert.That(SurvivalRules.UseItem(state, item, settings), Is.False);
            Assert.That(state.GetItemCount("stimulant"), Is.EqualTo(1));
            Assert.That(state.player.health, Is.EqualTo(100));
        }

        [Test]
        public void CalendarDayWeakensBothPlayerAndEnemyAndCapsAtCampaignEnd()
        {
            var state = new GameState(); SurvivalRules.Initialize(state, settings);
            SurvivalRules.AdvanceTime(state, 959, settings);
            Assert.That(state.player.maxHealth, Is.EqualTo(100));
            Assert.That(state.player.strength, Is.EqualTo(12));
            Assert.That(SurvivalRules.EnemyScale(state, settings), Is.EqualTo(1));
            SurvivalRules.AdvanceTime(state, 1, settings);
            Assert.That(state.player.maxHealth, Is.EqualTo(97));
            Assert.That(state.player.health, Is.EqualTo(97));
            Assert.That(state.player.strength, Is.EqualTo(11));
            Assert.That(SurvivalRules.EnemyScale(state, settings), Is.EqualTo(0.965f).Within(0.0001f));
            SurvivalRules.AdvanceTime(state, 100 * 1440, settings);
            Assert.That(state.player.maxHealth, Is.EqualTo(61));
            Assert.That(state.player.strength, Is.EqualTo(4));
            Assert.That(SurvivalRules.EnemyScale(state, settings), Is.EqualTo(0.545f).Within(0.0001f));
        }

        [Test]
        public void DeclineFloorsNeverIncreaseAlreadyLowBaselineStats()
        {
            var state = new GameState { player = new PlayerStats { maxHealth = 10, health = 10, strength = 2 } };
            SurvivalRules.AdvanceTime(state, 1440, settings);
            Assert.That(state.player.maxHealth, Is.EqualTo(10));
            Assert.That(state.player.strength, Is.EqualTo(2));
        }

        [Test]
        public void CluesActionsAndCompanionsUseTrimmedStableIdsWithoutDuplicates()
        {
            var state = new GameState();
            Assert.That(SurvivalRules.RecordClue(state, " witness ", "증언", "서로 다른 설명"), Is.True);
            Assert.That(SurvivalRules.RecordClue(state, "witness", "바뀐 증언", "중복"), Is.False);
            SurvivalRules.JoinCompanion(state, " friend "); SurvivalRules.JoinCompanion(state, "friend");
            SurvivalRules.UnlockAction(state, " reckless "); SurvivalRules.UnlockAction(state, "reckless");
            SurvivalRules.SetObjective(state, " destination ", "단서를 따라간다", 2);
            SurvivalRules.LeaveCompanion(state, " friend ");
            Assert.That(state.journey.clueIds, Is.EqualTo(new[] { "witness" }));
            Assert.That(state.journey.journal.Count, Is.EqualTo(1));
            Assert.That(state.journey.journal[0].title, Is.EqualTo("증언"));
            Assert.That(state.journey.objectiveId, Is.EqualTo("destination"));
            Assert.That(state.journey.stage, Is.EqualTo(2));
            Assert.That(state.party.activeCompanions, Is.Empty);
            Assert.That(state.party.knownCompanions, Is.EqualTo(new[] { "friend" }));
            Assert.That(state.unlockedActions.FindAll(id => id == "reckless").Count, Is.EqualTo(1));
        }

        [Test]
        public void NightActivityRequiresCompanionBeforeChangingTimeOrStats()
        {
            var state = new GameState(); state.player.health = 30;
            Assert.That(SurvivalRules.CompleteNightActivity(state, activity, settings), Is.False);
            Assert.That(state.survival.elapsedMinutes, Is.Zero);
            Assert.That(state.player.health, Is.EqualTo(30));
            Assert.That(state.journey.memories, Is.Empty);
        }

        [Test]
        public void NightActivityArrivesAtDawnRestoresHealthAndLeavesGoodMemoryAfterDeparture()
        {
            var state = new GameState(); state.player.health = 50; state.player.sanity = 20;
            settings.fatiguePerMinute = 0.001f; settings.nightMultiplier = 2;
            activity.memoryTitle = "같이 연주한 밤"; activity.memoryText = "형편없는 연주에도 웃었다.";
            SurvivalRules.JoinCompanion(state, "friend");
            int changes = 0; state.Changed += () => changes++;
            Assert.That(SurvivalRules.CompleteNightActivity(state, activity, settings), Is.True);
            Assert.That(changes, Is.EqualTo(1));
            Assert.That(state.survival.elapsedMinutes, Is.EqualTo(1320));
            Assert.That(SurvivalRules.Day(state, settings), Is.EqualTo(2));
            Assert.That(SurvivalRules.Hour(state, settings), Is.EqualTo(6));
            Assert.That(state.survival.fatigue, Is.EqualTo(0.96f).Within(0.0001f));
            Assert.That(state.player.health, Is.EqualTo(58));
            Assert.That(state.player.sanity, Is.EqualTo(35));
            SurvivalRules.ClearParty(state);
            Assert.That(state.party.activeCompanions, Is.Empty);
            Assert.That(state.party.knownCompanions, Is.EqualTo(new[] { "friend" }));
            Assert.That(state.journey.memories.Count, Is.EqualTo(1));
            Assert.That(state.journey.memories[0].text, Is.EqualTo("형편없는 연주에도 웃었다."));
        }

        [Test]
        public void ActivityStartingAfterMidnightUsesUpcomingDawn()
        {
            settings.initialHour = 1;
            activity.requiresCompanion = false;
            var state = new GameState();
            SurvivalRules.CompleteNightActivity(state, activity, settings);
            Assert.That(state.survival.elapsedMinutes, Is.EqualTo(300));
            Assert.That(SurvivalRules.Day(state, settings), Is.EqualTo(1));
            Assert.That(SurvivalRules.Hour(state, settings), Is.EqualTo(6));
        }

        [Test]
        public void RepeatingNightActivityRecordsDistinctNightsButOneEnduringMemory()
        {
            activity.requiresCompanion = false;
            var state = new GameState();
            SurvivalRules.CompleteNightActivity(state, activity, settings);
            SurvivalRules.CompleteNightActivity(state, activity, settings);
            Assert.That(SurvivalRules.Day(state, settings), Is.EqualTo(3));
            Assert.That(state.journey.memories.Count, Is.EqualTo(1));
            Assert.That(state.journey.journal.Count, Is.EqualTo(2));
            Assert.That(state.journey.journal[0].id, Is.Not.EqualTo(state.journey.journal[1].id));
        }

        [Test]
        public void MalformedNumbersAndHugeTimeCostsNormalizeWithoutOverflowOrMovementLock()
        {
            var state = new GameState(); state.survival.fatigue = float.NaN;
            settings.fatiguePerMinute = float.NaN; settings.nightMultiplier = float.PositiveInfinity;
            settings.healthLossPerDay = int.MaxValue; settings.strengthLossPerDay = int.MaxValue;
            SurvivalRules.AdvanceTime(state, int.MaxValue, settings, float.NaN);
            SurvivalRules.AdvanceTime(state, int.MaxValue, settings);
            Assert.That(state.survival.elapsedMinutes, Is.EqualTo(int.MaxValue));
            Assert.That(state.survival.fatigue, Is.EqualTo(100));
            Assert.That(state.player.maxHealth, Is.EqualTo(settings.minMaxHealth));
            Assert.That(state.player.strength, Is.EqualTo(settings.minStrength));
            Assert.That(SurvivalRules.MovementMultiplier(state, settings), Is.EqualTo(0.65f).Within(0.0001f));
            Assert.That(SurvivalRules.Day(state, settings), Is.GreaterThan(1));
        }

        [Test]
        public void SaturatedClockCannotGrantRecoveryFromIncompleteNight()
        {
            var state = new GameState(); SurvivalRules.Initialize(state, settings);
            state.survival.elapsedMinutes = int.MaxValue; state.player.health = 20;
            activity.requiresCompanion = false;
            Assert.That(SurvivalRules.CompleteNightActivity(state, activity, settings), Is.False);
            Assert.That(state.player.health, Is.EqualTo(20));
            Assert.That(state.journey.memories, Is.Empty);
        }

        [Test]
        public void NormalizeRepairsMissingJourneyCollectionsAndRetainsKnownActiveCompanions()
        {
            var state = new GameState();
            state.journey.clueIds = null; state.journey.journal = null; state.journey.memories = null;
            state.party.knownCompanions = null;
            state.party.activeCompanions.Add(" friend "); state.party.activeCompanions.Add("friend");
            state.party.activeCompanions.Add(null);
            state.Normalize();
            Assert.That(state.journey.clueIds, Is.Not.Null);
            Assert.That(state.journey.journal, Is.Not.Null);
            Assert.That(state.journey.memories, Is.Not.Null);
            Assert.That(state.party.activeCompanions, Is.EqualTo(new[] { "friend" }));
            Assert.That(state.party.knownCompanions, Is.EqualTo(new[] { "friend" }));
            Assert.Throws<ArgumentException>(() => SurvivalRules.RecordClue(state, " ", "", ""));
            Assert.Throws<ArgumentException>(() => SurvivalRules.JoinCompanion(state, null));
        }

        [Test]
        public void NonPositiveTimeCostsDoNotChangeState()
        {
            var state = new GameState(); int changes = 0; state.Changed += () => changes++;
            SurvivalRules.AdvanceTime(state, -1, settings);
            SurvivalRules.AdvanceTime(state, 0, settings);
            Assert.That(state.survival.elapsedMinutes, Is.Zero);
            Assert.That(state.survival.initialized, Is.False);
            Assert.That(changes, Is.Zero);
        }
    }
}
