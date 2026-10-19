using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace NoSleep.Tests
{
    public sealed class NpcEventRuntimeTests
    {
        readonly List<ScriptableObject> assets = new List<ScriptableObject>();
        GameObject host;
        GameSession session;
        MapDefinition map;
        EventDefinition npcEvent;
        EventObject owner;
        string saveDirectory;
        T Asset<T>() where T : ScriptableObject { var value = ScriptableObject.CreateInstance<T>(); assets.Add(value); return value; }
        [UnitySetUp]
        public IEnumerator Setup()
        {
            map = Asset<MapDefinition>(); map.mapId = "npc_commands"; map.Resize(8, 8); map.Fill(Asset<TileDefinition>()); map.spawnCell = Vector2Int.one;
            npcEvent = Asset<EventDefinition>(); npcEvent.pages.Add(new EventPage()); npcEvent.EnsureStableIds();
            var npc = Asset<NpcDefinition>(); npc.speed = 10; npc.EnsureStableIds();
            map.events.Add(new MapEventPlacement { id = "guide", cell = new Vector2Int(3, 3), definition = npcEvent, npc = npc });
            host = new GameObject("NPC event integration"); host.SetActive(false);
            var bootstrap = host.AddComponent<NoSleepBootstrap>(); bootstrap.startingMap = map; bootstrap.maps = new List<MapDefinition> { map }; bootstrap.showTitleScreen = false;
            host.SetActive(true); session = host.GetComponent<GameSession>();
            saveDirectory = Path.Combine(Path.GetTempPath(), "NoSleepNpcCombat_" + Guid.NewGuid().ToString("N"));
            session.ConfigureSaveDirectory(saveDirectory);
            Assert.That(session.IsReady, Is.True); owner = session.Map.EventObjects.Single(value => value.EventKey == "npc_commands/guide");
            yield return null;
        }
        [UnityTearDown]
        public IEnumerator Cleanup()
        { if (session != null) session.Scheduler.CancelAll(); if (host != null) Object.Destroy(host); yield return null; foreach (var value in assets) if (value != null) Object.Destroy(value); assets.Clear(); if (Directory.Exists(saveDirectory)) Directory.Delete(saveDirectory, true); yield return null; }
        IEnumerator Finish()
        { for (int frame = 0; frame < 240 && session.Events.IsRunning; frame++) { session.Npcs.Tick(0.1f); yield return null; } Assert.That(session.Events.IsRunning, Is.False, "NPC instruction exceeded its bounded frame budget."); }

        [UnityTest]
        public IEnumerator NestedCommonMovesAndFacesItsCallerBeforePayingReward()
        {
            var common = Asset<CommonEventDefinition>(); common.commands.Add(new EventCommand { type = EventCommandType.MoveNpc, cell = new Vector2Int(4, 3), boolValue = true });
            common.commands.Add(new EventCommand { type = EventCommandType.FaceNpc, intValue = 135, boolValue = true }); common.EnsureStableIds();
            npcEvent.pages[0].commands.Add(new EventCommand { type = EventCommandType.CallCommon, commonEvent = common });
            npcEvent.pages[0].commands.Add(new EventCommand { type = EventCommandType.AddGold, intValue = 11 }); npcEvent.EnsureStableIds();
            Assert.That(owner.TryInteract(), Is.True); yield return Finish();
            Assert.That(session.Events.CurrentContext.Succeeded, Is.True); Assert.That(session.State.player.gold, Is.EqualTo(11));
            Assert.That(session.Map.WorldToCell(owner.transform.position), Is.EqualTo(new Vector2Int(4, 3)));
            Assert.That(Mathf.DeltaAngle(owner.transform.eulerAngles.y, 135), Is.EqualTo(0).Within(0.01f));
            Assert.That(session.State.GetNpcState(map.mapId, "guide").persistentOverride, Is.True);
        }

        [UnityTest]
        public IEnumerator BlockedMoveCannotCompleteOncePageOrGiveFollowingGold()
        {
            var wall = Asset<TileDefinition>(); wall.walkable = false; map.SetTile(new Vector2Int(4, 3), wall);
            npcEvent.pages[0].runOnce = true; npcEvent.pages[0].commands.Add(new EventCommand { type = EventCommandType.MoveNpc, cell = new Vector2Int(4, 3) });
            npcEvent.pages[0].commands.Add(new EventCommand { type = EventCommandType.AddGold, intValue = 77 }); npcEvent.EnsureStableIds();
            LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: NPC movement failed"));
            Assert.That(owner.TryInteract(), Is.True); yield return Finish();
            Assert.That(session.Events.CurrentContext.Succeeded, Is.False); Assert.That(session.State.player.gold, Is.Zero);
            Assert.That(npcEvent.IsPageCompleted(session.State, owner.EventKey, 0), Is.False); Assert.That(session.Npcs.IsAtCellBoundary, Is.True);
            Assert.That(session.Map.WorldToCell(owner.transform.position), Is.EqualTo(new Vector2Int(3, 3)));
        }

        [UnityTest]
        public IEnumerator CancelledExplicitMovementReleasesReservationsAndAllowsRetry()
        {
            npcEvent.pages[0].commands.Add(new EventCommand { type = EventCommandType.MoveNpc, cell = new Vector2Int(6, 3) });
            npcEvent.pages[0].commands.Add(new EventCommand { type = EventCommandType.AddGold, intValue = 9 }); npcEvent.EnsureStableIds();
            Assert.That(owner.TryInteract(), Is.True); session.Npcs.Tick(0.02f); yield return null;
            session.Events.Cancel();
            Assert.That(session.State.player.gold, Is.Zero); Assert.That(session.Npcs.IsAtCellBoundary, Is.True);
            var result = NpcMoveResult.Cancelled;
            var retry = session.Npcs.MoveTo(owner.EventKey, new Vector2Int(4, 3), true, value => result = value);
            int pumps = 0; while (retry.MoveNext() && pumps++ < 100) { session.Npcs.Tick(0.1f); yield return null; }
            (retry as IDisposable)?.Dispose(); Assert.That(result, Is.EqualTo(NpcMoveResult.Succeeded));
        }

        [UnityTest]
        public IEnumerator StopAndResumeTogglePermanentOverrideForExplicitPlacementTarget()
        {
            npcEvent.pages[0].commands.Add(new EventCommand { type = EventCommandType.StopNpc, key = "guide", boolValue = true }); npcEvent.EnsureStableIds();
            Assert.That(owner.TryInteract(), Is.True); yield return Finish();
            Assert.That(session.State.GetNpcState(map.mapId, "guide").persistentOverride, Is.True);
            npcEvent.pages[0].commands = new List<EventCommand> { new EventCommand { type = EventCommandType.ResumeNpc, key = owner.EventKey } }; npcEvent.EnsureStableIds();
            Assert.That(owner.TryInteract(), Is.True); yield return Finish();
            Assert.That(session.Events.CurrentContext.Succeeded, Is.True); Assert.That(session.State.GetNpcState(map.mapId, "guide").persistentOverride, Is.False);
        }

        sealed class StrikePresentation : IEventPresentation
        {
            public int dialogues;
            public IEnumerator ShowDialogue(string speaker, string text) { dialogues++; yield break; }
            public IEnumerator ShowChoices(string prompt, List<string> choices, Action<int> selected) { selected(0); yield break; }
        }

        [UnityTest]
        public IEnumerator DebuggerStepFinishesOnlyOwnedNpcCommandBeforeNextReward()
        {
            var route = Asset<NpcRouteDefinition>(); route.points.Add(new NpcRoutePoint { cell = new Vector2Int(5, 5) });
            route.points.Add(new NpcRoutePoint { cell = new Vector2Int(6, 5) }); route.EnsureStableIds();
            var other = Asset<NpcDefinition>(); other.route = route; other.EnsureStableIds();
            map.events.Add(new MapEventPlacement { id = "other", npc = other, cell = new Vector2Int(5, 5) });
            npcEvent.pages[0].commands.Add(new EventCommand { type = EventCommandType.MoveNpc, cell = new Vector2Int(4, 3), boolValue = true });
            npcEvent.pages[0].commands.Add(new EventCommand { type = EventCommandType.AddGold, intValue = 11 }); npcEvent.EnsureStableIds();
            session.Map.Load(map); owner = session.Map.EventObjects.Single(value => value.EventKey == "npc_commands/guide");
            Assert.That(owner.TryInteract(), Is.True); session.Scheduler.SetPaused(true);
            session.Npcs.Tick(1); Assert.That(session.Npcs.Find("guide").Cell, Is.EqualTo(new Vector2Int(3, 3)));
            session.Scheduler.Step();
            for (int frame = 0; frame < 40 && session.Npcs.Find("guide").Cell != new Vector2Int(4, 3); frame++)
            { session.Npcs.Tick(.1f); yield return null; }
            Assert.That(session.Npcs.Find("guide").Cell, Is.EqualTo(new Vector2Int(4, 3)));
            yield return null; yield return null;
            Assert.That(session.State.player.gold, Is.Zero); Assert.That(session.Scheduler.Paused, Is.True);
            Assert.That(session.Npcs.Find("other").Cell, Is.EqualTo(new Vector2Int(5, 5))); Assert.That(session.Npcs.Find("other").HasSegment, Is.False);
            session.Scheduler.Step();
            for (int frame = 0; frame < 10 && session.Events.IsRunning; frame++) yield return null;
            Assert.That(session.State.player.gold, Is.EqualTo(11)); Assert.That(session.Events.IsRunning, Is.False);
        }

        [UnityTest]
        public IEnumerator TemporaryFacingFinishesCurrentCellAndRestoresAutomaticMovement()
        {
            var route = Asset<NpcRouteDefinition>(); route.points.Add(new NpcRoutePoint { cell = new Vector2Int(3, 3) });
            route.points.Add(new NpcRoutePoint { cell = new Vector2Int(5, 3) }); route.EnsureStableIds();
            map.events[0].npc.route = route;
            npcEvent.pages[0].commands.Add(new EventCommand { type = EventCommandType.FaceNpc, intValue = 45, boolValue = false });
            npcEvent.pages[0].commands.Add(new EventCommand { type = EventCommandType.Wait, seconds = 10 }); npcEvent.EnsureStableIds();
            session.Map.Load(map); owner = session.Map.EventObjects.Single(value => value.EventKey == "npc_commands/guide");
            session.Npcs.Tick(0); session.Npcs.Tick(0); Assert.That(session.Npcs.Find("guide").HasSegment, Is.True);
            Assert.That(owner.TryInteract(), Is.True);
            for (int frame = 0; frame < 40 && session.Events.CurrentContext.CommandIndex != 1; frame++)
            { session.Npcs.Tick(.1f); yield return null; }
            var actor = session.Npcs.Find("guide");
            Assert.That(session.Events.CurrentContext.CommandIndex, Is.EqualTo(1)); Assert.That(actor.HasSegment, Is.False);
            Assert.That(actor.Cell, Is.EqualTo(new Vector2Int(4, 3))); Assert.That(actor.Yaw, Is.EqualTo(45)); Assert.That(actor.PersistentOverride, Is.False);
            session.Events.Cancel(); session.Npcs.Tick(0);
            Assert.That(actor.HasSegment, Is.True, "A temporary facing command preserves the previous automatic movement state.");
        }

        [UnityTest]
        public IEnumerator ApproachingEnemyVictoryRemainsDormantAfterSaveAndLoad()
        {
            var settings = Asset<SurvivalSettings>(); settings.timeMode = TimeMode.AuthoredCosts;
            session.ConfigureHorror(settings, null);
            var enemy = Asset<EnemyDefinition>(); enemy.baseHealth = 1; enemy.baseAttack = 1;
            var npc = map.events[0].npc; npc.enemy = enemy; npc.speed = 1; npc.detectionRadius = 6; npc.leashRadius = 8;
            npcEvent.pages[0].trigger = EventTrigger.PlayerTouch; npcEvent.pages[0].blocksMovement = false;
            npcEvent.pages[0].commands.Add(new EventCommand { type = EventCommandType.StartEncounter, enemy = enemy, key = "defeated" });
            npcEvent.pages.Add(new EventPage { visible = false, blocksMovement = false,
                conditions = new List<StateCondition> { new StateCondition { kind = ConditionKind.SelfSwitch, key = "defeated" } } });
            npcEvent.EnsureStableIds(); session.Map.Load(map);
            var presentation = new StrikePresentation(); session.Events.Configure(session, presentation); session.Combat.Configure(session, presentation);
            session.Player.Warp(session.Map.CellToWorld(new Vector2Int(5, 3)), 0);
            bool approached = false; Vector3 start = session.Npcs.Find("guide").transform.position;
            for (int frame = 0; frame < 180 && !session.State.GetSelfSwitch("npc_commands/guide", "defeated"); frame++)
            { session.Npcs.Tick(.1f); approached |= Vector3.Distance(start, session.Npcs.Find("guide").transform.position) > .25f; yield return null; }
            Assert.That(session.State.GetSelfSwitch("npc_commands/guide", "defeated"), Is.True, "The approaching actor must trigger and finish a real encounter.");
            yield return Finish(); session.Npcs.Tick(.1f);
            Assert.That(approached, Is.True, "The enemy must approach before contact, even when defeat cancels its unfinished step.");
            Assert.That(session.Npcs.Find("guide").IsActive, Is.False);
            var defeatedCell = session.Npcs.Find("guide").Cell;
            Assert.That(session.Save(), Is.True); Assert.That(session.SaveStatus, Is.EqualTo(SaveRequestStatus.Completed));
            Assert.That(session.Load(), Is.True); session.Npcs.Tick(.1f);
            Assert.That(session.Npcs.Find("guide").IsActive, Is.False); Assert.That(session.Npcs.Find("guide").Cell, Is.EqualTo(defeatedCell));
            var dormantOwner = session.Map.EventObjects.Single(value => value.EventKey == "npc_commands/guide");
            Assert.That(dormantOwner.CanInteract, Is.False); Assert.That(dormantOwner.TryTouch(), Is.False);
            int count = presentation.dialogues;
            for (int frame = 0; frame < 10; frame++) { session.Npcs.Tick(.1f); yield return null; }
            Assert.That(presentation.dialogues, Is.EqualTo(count)); Assert.That(session.Combat.IsRunning, Is.False);
        }
    }
}
