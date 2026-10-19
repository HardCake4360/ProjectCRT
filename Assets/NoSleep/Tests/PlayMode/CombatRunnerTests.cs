using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace NoSleep.Tests
{
    public sealed class CombatRunnerTests
    {
        GameObject host;
        GameSession session;
        CombatRunner combat;
        EventRunner events;
        EnemyDefinition enemy;
        SurvivalSettings settings;
        ItemEffectDefinition stimulant;
        EventDefinition eventDefinition;
        FakePresentation presentation;
        IEnumerator combatIterator;
        Coroutine combatRoutine;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("Combat test");
            var stateHost = new GameObject("Isolated combat state");
            stateHost.SetActive(false);
            stateHost.transform.SetParent(host.transform);
            session = stateHost.AddComponent<GameSession>();
            combat = host.AddComponent<CombatRunner>();
            events = host.AddComponent<EventRunner>();
            SetSessionDependency(nameof(GameSession.Combat), combat);
            SetSessionDependency(nameof(GameSession.Events), events);
            settings = ScriptableObject.CreateInstance<SurvivalSettings>();
            settings.fatiguePerMinute = 0;
            stimulant = ScriptableObject.CreateInstance<ItemEffectDefinition>();
            session.ConfigureHorror(settings, stimulant);
            presentation = new FakePresentation();
            combat.Configure(session, presentation);
            events.Configure(session, presentation);
            enemy = ScriptableObject.CreateInstance<EnemyDefinition>();
            eventDefinition = ScriptableObject.CreateInstance<EventDefinition>();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (events != null) events.Cancel();
            if (combatRoutine != null && combat != null) combat.StopCoroutine(combatRoutine);
            (combatIterator as IDisposable)?.Dispose();
            UnityEngine.Object.Destroy(host);
            UnityEngine.Object.Destroy(enemy);
            UnityEngine.Object.Destroy(settings);
            UnityEngine.Object.Destroy(stimulant);
            UnityEngine.Object.Destroy(eventDefinition);
            yield return null;
        }

        [UnityTest]
        public IEnumerator VictoryEndsAfterDialogueAndChargesOneTurnWithoutGrantingRewards()
        {
            enemy.baseHealth = 8;
            CombatResult? result = null;
            StartEncounter(value => result = value);
            Assert.That(combat.IsRunning, Is.True);
            Assert.That(session.IsInputBlocked, Is.True);
            yield return FinishWithin(20);
            Assert.That(result, Is.EqualTo(CombatResult.Victory));
            Assert.That(session.State.player.health, Is.EqualTo(100));
            Assert.That(session.State.survival.elapsedMinutes, Is.EqualTo(5));
            Assert.That(session.State.player.gold, Is.Zero);
            Assert.That(session.IsGameplayBusy, Is.False);
            Assert.That(session.IsInputBlocked, Is.False);
        }

        [UnityTest]
        public IEnumerator FleeReportsEscapeAndDoesNotGrantRewardOrDefeatFlag()
        {
            presentation.SelectedLabel = "물러나기";
            CombatResult? result = null;
            StartEncounter(value => result = value);
            yield return FinishWithin(20);
            Assert.That(result, Is.EqualTo(CombatResult.Escaped));
            Assert.That(session.State.player.health, Is.EqualTo(100));
            Assert.That(session.State.player.gold, Is.Zero);
            Assert.That(session.State.survival.elapsedMinutes, Is.EqualTo(5));
            Assert.That(session.State.GetSelfSwitch("test/enemy", "defeated"), Is.False);
        }

        [UnityTest]
        public IEnumerator DefeatReportsAfterFinalDialogueAndReleasesInput()
        {
            enemy.baseHealth = 100;
            enemy.baseAttack = 100;
            session.State.player.health = 5;
            CombatResult? result = null;
            bool callbackAfterDialogue = false;
            StartEncounter(value => { result = value; callbackAfterDialogue = presentation.LastDialogueDisposed; });
            yield return FinishWithin(20);
            Assert.That(result, Is.EqualTo(CombatResult.Defeat));
            Assert.That(callbackAfterDialogue, Is.True);
            Assert.That(session.State.player.health, Is.Zero);
            Assert.That(session.State.survival.elapsedMinutes, Is.EqualTo(5));
            Assert.That(session.IsGameplayBusy, Is.False);
            Assert.That(session.IsInputBlocked, Is.True, "Defeat must stop player control until load or restart.");
        }

        [UnityTest]
        public IEnumerator EventVictoryPersistsPlacementDefeatAfterEncounterFinishes()
        {
            enemy.baseHealth = 8;
            AddEncounterPage(false);
            Assert.That(events.TryRun(eventDefinition, "test/enemy"), Is.True);
            for (int frame = 0; frame < 20 && events.IsRunning; frame++) yield return null;
            Assert.That(events.IsRunning, Is.False);
            Assert.That(combat.IsRunning, Is.False);
            Assert.That(session.State.GetSelfSwitch("test/enemy", "defeated"), Is.True);
            var restored = JsonUtility.FromJson<GameState>(JsonUtility.ToJson(session.State));
            restored.Normalize();
            Assert.That(restored.GetSelfSwitch("test/enemy", "defeated"), Is.True);
        }

        [UnityTest]
        public IEnumerator EventEscapeDoesNotCompleteRunOncePageOrExecutePostCombatReward()
        {
            presentation.SelectedLabel = "물러나기";
            AddEncounterPage(true);
            eventDefinition.pages[0].commands.Add(new EventCommand { type = EventCommandType.AddGold, intValue = 99 });
            Assert.That(events.TryRun(eventDefinition, "test/enemy"), Is.True);
            for (int frame = 0; frame < 20 && events.IsRunning; frame++) yield return null;
            Assert.That(events.IsRunning, Is.False);
            Assert.That(combat.IsRunning, Is.False);
            Assert.That(session.State.GetSelfSwitch("test/enemy", "defeated"), Is.False);
            Assert.That(eventDefinition.IsPageCompleted(session.State, "test/enemy", 0), Is.False);
            Assert.That(session.State.player.gold, Is.Zero);
            Assert.That(session.State.survival.elapsedMinutes, Is.EqualTo(5));
            Assert.That(events.TryRun(eventDefinition, "test/enemy"), Is.True, "A later entry must still permit the encounter.");
            events.Cancel();
        }

        [UnityTest]
        public IEnumerator EventCancellationDisposesPendingCombatChoiceAndReleasesBothRunners()
        {
            presentation.HoldChoices = true;
            AddEncounterPage(true);
            Assert.That(events.TryRun(eventDefinition, "test/enemy"), Is.True);
            for (int frame = 0; frame < 10 && !presentation.ChoiceEntered; frame++) yield return null;
            Assert.That(presentation.ChoiceEntered, Is.True);
            Assert.That(combat.IsRunning, Is.True);
            Assert.That(events.IsRunning, Is.True);
            events.Cancel();
            Assert.That(combat.IsRunning, Is.False);
            Assert.That(events.IsRunning, Is.False);
            Assert.That(session.IsInputBlocked, Is.False);
            Assert.That(presentation.ChoiceDisposed, Is.True);
            Assert.That(session.State.GetSelfSwitch("test/enemy", "defeated"), Is.False);
            Assert.That(eventDefinition.IsPageCompleted(session.State, "test/enemy", 0), Is.False);
            Assert.That(session.State.survival.elapsedMinutes, Is.Zero);
        }

        void StartEncounter(Action<CombatResult> completed)
        {
            combatIterator = combat.Run(enemy, completed);
            combatRoutine = combat.StartCoroutine(combatIterator);
        }

        void AddEncounterPage(bool runOnce)
        {
            eventDefinition.pages.Add(new EventPage
            {
                runOnce = runOnce,
                commands = new List<EventCommand>
                {
                    new EventCommand { type = EventCommandType.StartEncounter, enemy = enemy, key = "defeated" }
                }
            });
        }

        void SetSessionDependency(string property, object value) => typeof(GameSession).GetProperty(property)
            .GetSetMethod(true).Invoke(session, new[] { value });

        IEnumerator FinishWithin(int frames)
        {
            for (int frame = 0; frame < frames && combat.IsRunning; frame++) yield return null;
            Assert.That(combat.IsRunning, Is.False, "Combat did not finish within the frame budget.");
        }

        sealed class FakePresentation : IEventPresentation
        {
            public string SelectedLabel = "공격";
            public bool HoldChoices;
            public bool ChoiceEntered;
            public bool ChoiceDisposed;
            public bool LastDialogueDisposed;

            public IEnumerator ShowDialogue(string speaker, string text)
            {
                LastDialogueDisposed = false;
                try { yield return null; }
                finally { LastDialogueDisposed = true; }
            }

            public IEnumerator ShowChoices(string prompt, List<string> choices, Action<int> onSelected)
            {
                try
                {
                    ChoiceEntered = true;
                    do { yield return null; } while (HoldChoices);
                    int selected = choices.IndexOf(SelectedLabel);
                    if (selected < 0) throw new InvalidOperationException("Test choice is missing: " + SelectedLabel);
                    onSelected(selected);
                }
                finally { ChoiceDisposed = true; }
            }
        }
    }
}
