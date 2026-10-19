using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace NoSleep.Tests
{
    public sealed class EventProgressionTests
    {
        GameObject host;
        GameSession session;
        EventRunner runner;
        SurvivalSettings settings;
        ItemEffectDefinition item;
        NightActivityDefinition activity;
        EventDefinition definition;
        readonly List<ScriptableObject> extraAssets = new List<ScriptableObject>();

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("Event progression test");
            var stateHost = new GameObject("Isolated progression state");
            stateHost.SetActive(false);
            stateHost.transform.SetParent(host.transform);
            session = stateHost.AddComponent<GameSession>();
            runner = host.AddComponent<EventRunner>();
            settings = ScriptableObject.CreateInstance<SurvivalSettings>();
            settings.fatiguePerMinute = 0;
            item = ScriptableObject.CreateInstance<ItemEffectDefinition>();
            activity = ScriptableObject.CreateInstance<NightActivityDefinition>();
            session.ConfigureHorror(settings, item);
            runner.Configure(session, new FakePresentation());
            definition = ScriptableObject.CreateInstance<EventDefinition>();
            definition.pages.Add(new EventPage { runOnce = true });
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (runner != null) runner.Cancel();
            UnityEngine.Object.Destroy(host);
            UnityEngine.Object.Destroy(settings);
            UnityEngine.Object.Destroy(item);
            UnityEngine.Object.Destroy(activity);
            UnityEngine.Object.Destroy(definition);
            foreach (var asset in extraAssets) UnityEngine.Object.Destroy(asset);
            extraAssets.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator FailedItemUseLeavesPageReplayableAndSuccessfulUseCompletesOnlyOnce()
        {
            const string eventKey = "test/useItem";
            definition.pages[0].commands = new List<EventCommand>
            {
                new EventCommand { type = EventCommandType.UseItem, item = item },
                new EventCommand { type = EventCommandType.AddGold, intValue = 7 }
            };
            Assert.That(runner.TryRun(definition, eventKey), Is.True);
            Assert.That(runner.IsRunning, Is.False);
            Assert.That(definition.IsPageCompleted(session.State, eventKey, 0), Is.False);
            Assert.That(session.State.player.gold, Is.Zero);
            Assert.That(session.State.player.health, Is.EqualTo(100));
            Assert.That(session.State.survival.awakenedUntilMinutes, Is.Zero);

            session.State.AddItem(item.itemId, 1);
            Assert.That(runner.TryRun(definition, eventKey), Is.True);
            Assert.That(runner.IsRunning, Is.False);
            Assert.That(session.State.GetItemCount(item.itemId), Is.Zero);
            Assert.That(session.State.player.health, Is.EqualTo(100 - item.healthCost));
            Assert.That(session.State.player.gold, Is.EqualTo(7));
            Assert.That(definition.IsPageCompleted(session.State, eventKey, 0), Is.True);
            Assert.That(runner.TryRun(definition, eventKey), Is.False);
            Assert.That(session.State.player.gold, Is.EqualTo(7));
            yield return null;
        }

        [UnityTest]
        public IEnumerator MissingCompanionLeavesNightActivityReplayableAndSuccessRecordsOneMemory()
        {
            const string eventKey = "test/night";
            activity.requiresCompanion = true;
            definition.pages[0].commands = new List<EventCommand>
            {
                new EventCommand { type = EventCommandType.StayAwake, nightActivity = activity },
                new EventCommand { type = EventCommandType.AddGold, intValue = 11 }
            };
            Assert.That(runner.TryRun(definition, eventKey), Is.True);
            Assert.That(runner.IsRunning, Is.False);
            Assert.That(definition.IsPageCompleted(session.State, eventKey, 0), Is.False);
            Assert.That(session.State.player.gold, Is.Zero);
            Assert.That(session.State.survival.elapsedMinutes, Is.Zero);
            Assert.That(session.State.journey.memories, Is.Empty);

            SurvivalRules.JoinCompanion(session.State, "test_companion");
            Assert.That(runner.TryRun(definition, eventKey), Is.True);
            Assert.That(runner.IsRunning, Is.False);
            Assert.That(session.State.survival.elapsedMinutes, Is.GreaterThan(0));
            Assert.That(SurvivalRules.Hour(session.State, settings), Is.EqualTo(activity.dawnHour));
            Assert.That(session.State.journey.memories.Count, Is.EqualTo(1));
            Assert.That(session.State.journey.memories[0].id, Is.EqualTo(activity.activityId));
            Assert.That(session.State.player.gold, Is.EqualTo(11));
            Assert.That(definition.IsPageCompleted(session.State, eventKey, 0), Is.True);
            Assert.That(runner.TryRun(definition, eventKey), Is.False);
            Assert.That(session.State.journey.memories.Count, Is.EqualTo(1));
            Assert.That(session.State.player.gold, Is.EqualTo(11));
            yield return null;
        }

        [UnityTest]
        public IEnumerator TopModalDisablesCoveredButtonsAndClosingItRestoresKeyboardFocus()
        {
            if (GameSession.Instance != null) Assert.Ignore("Real UI test preserves an existing running game.");
            var floor = ScriptableObject.CreateInstance<TileDefinition>();
            var map = ScriptableObject.CreateInstance<MapDefinition>();
            extraAssets.Add(floor); extraAssets.Add(map);
            map.mapId = "test_modal_map";
            map.Resize(3, 3);
            map.Fill(floor);
            map.spawnCell = new Vector2Int(1, 1);
            var mapHost = new GameObject("Modal test map"); mapHost.transform.SetParent(host.transform);
            var runtime = mapHost.AddComponent<TileMapRuntime>();
            var playerHost = new GameObject("Modal test player"); playerHost.transform.SetParent(host.transform);
            var player = playerHost.AddComponent<PlayerController>();
            var ui = host.AddComponent<GameUI>();
            session.ConfigureSaveDirectory(System.IO.Path.Combine(Application.temporaryCachePath, "NoSleepModalTest_" + Guid.NewGuid().ToString("N")));
            session.Configure(runtime, player, runner, ui, new[] { map });
            Assert.That(session.TransferPlayer(map, map.spawnCell), Is.True);
            ui.ShowTitle();
            ui.ShowSaveSlots(false);
            yield return null;

            var canvas = host.transform.Find("NoSleep Canvas");
            Assert.That(canvas, Is.Not.Null);
            var title = canvas.Find("Title Screen");
            var slots = canvas.Find("Save Slots");
            var newGame = title.Find("New Game").GetComponent<Button>();
            var back = slots.Find("Back").GetComponent<Button>();
            Assert.That(newGame.IsInteractable(), Is.False, "Covered title buttons must be excluded from automatic keyboard navigation.");
            Assert.That(back.IsInteractable(), Is.True);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(back.gameObject));
            back.onClick.Invoke();
            yield return null;
            Assert.That(newGame.IsInteractable(), Is.True);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(newGame.gameObject));

            ui.ShowGameOver();
            yield return null;
            var gameOver = canvas.Find("Game Over");
            var restart = gameOver.Find("Restart").GetComponent<Button>();
            var load = gameOver.Find("Load").GetComponent<Button>();
            restart.onClick.Invoke();
            yield return null;
            var confirm = canvas.Find("Confirm");
            var cancel = confirm.Find("Cancel").GetComponent<Button>();
            Assert.That(load.IsInteractable(), Is.False, "Covered game-over buttons must be excluded while confirmation is open.");
            Assert.That(restart.IsInteractable(), Is.False);
            Assert.That(cancel.IsInteractable(), Is.True);
            cancel.onClick.Invoke();
            yield return null;
            Assert.That(load.IsInteractable(), Is.True);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(load.gameObject));
        }

        sealed class FakePresentation : IEventPresentation
        {
            public IEnumerator ShowDialogue(string speaker, string text) { yield break; }
            public IEnumerator ShowChoices(string prompt, List<string> choices, Action<int> onSelected)
            { onSelected(0); yield break; }
        }
    }
}
