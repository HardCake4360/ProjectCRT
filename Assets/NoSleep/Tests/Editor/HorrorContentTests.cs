using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;

namespace NoSleep.Tests
{
    public sealed class HorrorContentTests
    {
        const string Data = "Assets/NoSleep/Data/Horror/";
        const string Scene = "Assets/NoSleep/Scenes/NoSleepHorrorPrototype.unity";

        [Test]
        public void PrototypeMapsAndEveryTransferAreValidAndReachableFromStartingMap()
        {
            var maps = new[] { Load<MapDefinition>("Maps/Apartment"), Load<MapDefinition>("Maps/Hospital"),
                Load<MapDefinition>("Maps/Transit"), Load<MapDefinition>("Maps/Approach") };
            Assert.That(maps.Select(map => map.mapId).Distinct().Count(), Is.EqualTo(4));
            var reachable = new HashSet<MapDefinition> { maps[0] };
            var queue = new Queue<MapDefinition>(); queue.Enqueue(maps[0]);
            foreach (var map in maps)
            {
                Assert.That(map.Validate(), Is.Empty, map.mapId);
                foreach (var placement in map.events)
                    foreach (var page in placement.definition.pages)
                        Assert.DoesNotThrow(() => EventProgram.ValidateAndIndex(page.commands), placement.id);
            }
            while (queue.Count > 0)
            {
                var map = queue.Dequeue();
                foreach (var command in map.events.SelectMany(placement => placement.definition.pages).SelectMany(page => page.commands))
                    if (command.type == EventCommandType.TransferPlayer)
                    {
                        Assert.That(maps, Does.Contain(command.map));
                        Assert.That(command.map.IsWalkable(command.cell), Is.True);
                        if (reachable.Add(command.map)) queue.Enqueue(command.map);
                    }
            }
            Assert.That(reachable.Count, Is.EqualTo(4));
        }

        [Test]
        public void SceneIncludesEveryProfileCompanionEnemyAndNightActivity()
        {
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(Scene), Is.Not.Null);
            var dependencies = AssetDatabase.GetDependencies(Scene, true);
            foreach (string relative in new[] { "Rules/SurvivalSettings", "Rules/Stimulant", "Rules/BandNight", "Rules/RollerCoasterNight",
                "Companions/Min", "Companions/Jun", "Enemies/HallwayPresence", "Enemies/FadingPresence", "Maps/Apartment", "Maps/Hospital", "Maps/Transit", "Maps/Approach" })
                Assert.That(dependencies, Does.Contain(Data + relative + ".asset"), relative);
            Assert.That(Load<SurvivalSettings>("Rules/SurvivalSettings").profileId, Is.EqualTo("no_sleep_horror_prototype"));
        }

        [Test]
        public void HospitalExitUsesAcquisitionFlagAfterAllMedicineHasBeenConsumed()
        {
            var state = new GameState(); state.SetSwitch("meds_found", true);
            var exit = Load<EventDefinition>("Events/HospitalExit");
            var gate = exit.pages[0].commands.First(command => command.type == EventCommandType.IfCondition);
            Assert.That(state.GetItemCount("stimulant"), Is.Zero);
            Assert.That(gate.condition.kind, Is.EqualTo(ConditionKind.Switch));
            Assert.That(gate.condition.key, Is.EqualTo("meds_found"));
            Assert.That(gate.condition.Evaluate(state, "horror_hospital/transit_door"), Is.True);
        }

        [TestCase("MedicineCabinet")]
        [TestCase("Doctor")]
        [TestCase("Min")]
        [TestCase("Jun")]
        [TestCase("HospitalExit")]
        [TestCase("TransitExit")]
        [TestCase("Destination")]
        public void CompletedPagesCannotDuplicateRewardsRejoinOrRegressObjectives(string file)
        {
            var state = new GameState(); state.SetSelfSwitch("guard", "A", true);
            var definition = Load<EventDefinition>("Events/" + file);
            Assert.That(definition.pages[0].commands.Any(command => command.type == EventCommandType.SetSelfSwitch && command.key == "A"), Is.True);
            var completed = definition.ResolvePage(state, "guard");
            Assert.That(completed, Is.SameAs(definition.pages.Last()));
            Assert.That(completed.commands.Any(command => command.type == EventCommandType.AddItem || command.type == EventCommandType.JoinCompanion
                || command.type == EventCommandType.SetObjective || command.type == EventCommandType.ClearParty), Is.False);
        }

        [Test]
        public void BothGoodNightOptionsRemainConnectedAndHaveNoEncounterCommand()
        {
            var gathering = Load<EventDefinition>("Events/NightGathering");
            var commands = gathering.pages.SelectMany(page => page.commands).ToArray();
            var activities = commands.Where(command => command.type == EventCommandType.StayAwake).Select(command => command.nightActivity).ToArray();
            Assert.That(activities.Length, Is.EqualTo(2));
            Assert.That(activities.Select(activity => activity.activityId), Is.EquivalentTo(new[] { "band_night", "roller_coaster_night" }));
            Assert.That(activities.All(activity => activity.requiresCompanion && !string.IsNullOrWhiteSpace(activity.memoryText)), Is.True);
            Assert.That(commands.Any(command => command.type == EventCommandType.StartEncounter), Is.False);
            var state = new GameState(); SurvivalRules.JoinCompanion(state, "min");
            foreach (var activity in activities)
                Assert.That(SurvivalRules.CompleteNightActivity(state, activity, Load<SurvivalSettings>("Rules/SurvivalSettings")), Is.True);
            SurvivalRules.ClearParty(state);
            Assert.That(state.journey.memories.Select(memory => memory.id), Is.EquivalentTo(new[] { "band_night", "roller_coaster_night" }));
        }

        [TestCase("HospitalMonster")]
        [TestCase("TransitMonster")]
        [TestCase("ApproachMonster")]
        public void VisibleContactEnemyPersistsUntilVictoryThenStopsBlockingMovement(string file)
        {
            var definition = Load<EventDefinition>("Events/" + file);
            var state = new GameState(); var active = definition.ResolvePage(state, "monster");
            Assert.That(active.trigger, Is.EqualTo(EventTrigger.PlayerTouch));
            Assert.That(active.visible, Is.True);
            Assert.That(active.runOnce, Is.False);
            var command = active.commands.Single(value => value.type == EventCommandType.StartEncounter);
            Assert.That(command.enemy, Is.Not.Null);
            Assert.That(command.key, Is.EqualTo("defeated"));
            state.SetSelfSwitch("monster", command.key, true);
            var defeated = definition.ResolvePage(state, "monster");
            Assert.That(defeated.visible, Is.False);
            Assert.That(defeated.blocksMovement, Is.False);
        }

        [Test]
        public void FinalApproachRequiresTwoAccountsAndGoodNightBeforeCompanionDeparture()
        {
            var commands = Load<EventDefinition>("Events/TransitExit").pages[0].commands;
            var conditions = commands.Where(command => command.type == EventCommandType.IfCondition).Select(command => command.condition).ToArray();
            Assert.That(conditions.Length, Is.EqualTo(3));
            var state = new GameState();
            Assert.That(conditions.All(condition => condition.Evaluate(state, "exit")), Is.False);
            SurvivalRules.RecordClue(state, "hospital_account", "병원", "병원의 설명");
            SurvivalRules.RecordClue(state, "transit_account", "환승 구역", "다른 설명, 같은 방향");
            Assert.That(conditions.All(condition => condition.Evaluate(state, "exit")), Is.False);
            state.SetSwitch("night_completed", true);
            Assert.That(conditions.All(condition => condition.Evaluate(state, "exit")), Is.True);
            int departure = commands.FindIndex(command => command.type == EventCommandType.ClearParty);
            int transfer = commands.FindIndex(command => command.type == EventCommandType.TransferPlayer);
            Assert.That(departure, Is.GreaterThan(2));
            Assert.That(transfer, Is.GreaterThan(departure));
            Assert.That(commands.Any(command => command.type == EventCommandType.JoinCompanion || command.type == EventCommandType.ReachDestination), Is.False);
            var destination = Load<EventDefinition>("Events/Destination");
            Assert.That(destination.pages[0].commands.Any(command => command.type == EventCommandType.ReachDestination), Is.True);
        }

        static T Load<T>(string path) where T : UnityEngine.Object
        {
            var value = AssetDatabase.LoadAssetAtPath<T>(Data + path + ".asset");
            Assert.That(value, Is.Not.Null, "Create the No Sleep Horror Prototype before content tests: " + path);
            return value;
        }
    }
}
