using System;
using NUnit.Framework;

namespace NoSleep.Tests
{
    public sealed class CoreTests
    {
        [Test]
        public void StateKeysTrimAndUpdateExistingEntries()
        {
            var state = new GameState();
            int changes = 0;
            state.Changed += () => changes++;
            state.SetSwitch(" door ", true);
            state.SetSwitch("door", false);
            state.SetVariable(" chapter ", 2);
            state.AddVariable("chapter", 3);
            state.AddItem(" key ", 1);
            state.AddItem("key", 2);

            Assert.That(state.GetSwitch(" door "), Is.False);
            Assert.That(state.GetVariable(" chapter "), Is.EqualTo(5));
            Assert.That(state.GetItemCount(" key "), Is.EqualTo(3));
            Assert.That(state.switches.Count, Is.EqualTo(1));
            Assert.That(state.variables.Count, Is.EqualTo(1));
            Assert.That(state.inventory.Count, Is.EqualTo(1));
            Assert.That(changes, Is.EqualTo(6));
        }

        [Test]
        public void SelfSwitchesRemainIndependentWhenIdentifiersContainSeparators()
        {
            var state = new GameState();
            state.SetSelfSwitch(" a:b ", " c ", true);
            state.SetSelfSwitch("a", "b:c", false);

            Assert.That(state.GetSelfSwitch("a:b", "c"), Is.True);
            Assert.That(state.GetSelfSwitch("a", "b:c"), Is.False);
            Assert.That(state.GetSelfSwitch("another-event", "c"), Is.False);
            Assert.That(state.selfSwitches.Count, Is.EqualTo(2));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void EmptyWriteKeysFailWithoutMutatingState(string key)
        {
            var state = new GameState();
            Assert.Throws<ArgumentException>(() => state.SetSwitch(key, true));
            Assert.Throws<ArgumentException>(() => state.SetVariable(key, 2));
            Assert.Throws<ArgumentException>(() => state.AddItem(key, 1));
            Assert.Throws<ArgumentException>(() => state.SetSelfSwitch(key, "A", true));
            Assert.Throws<ArgumentException>(() => state.SetSelfSwitch("event", key, true));
            Assert.That(state.switches, Is.Empty);
            Assert.That(state.variables, Is.Empty);
            Assert.That(state.inventory, Is.Empty);
            Assert.That(state.selfSwitches, Is.Empty);
        }

        [Test]
        public void InventoryAndGoldClampAtZeroAndMaximumWithoutOverflow()
        {
            var state = new GameState();
            state.AddItem("coin", int.MaxValue);
            state.AddItem("coin", int.MaxValue);
            state.AddGold(int.MaxValue);
            state.AddGold(int.MaxValue);
            Assert.That(state.GetItemCount("coin"), Is.EqualTo(int.MaxValue));
            Assert.That(state.player.gold, Is.EqualTo(int.MaxValue));

            state.AddItem("coin", int.MinValue);
            state.AddGold(int.MinValue);
            Assert.That(state.GetItemCount("coin"), Is.Zero);
            Assert.That(state.player.gold, Is.Zero);
        }

        [Test]
        public void VariableOverflowIsRejectedWithoutChangingValue()
        {
            var state = new GameState();
            state.SetVariable("score", int.MaxValue);
            Assert.Throws<OverflowException>(() => state.AddVariable("score", 1));
            Assert.That(state.GetVariable("score"), Is.EqualTo(int.MaxValue));
            state.SetVariable("score", int.MinValue);
            Assert.Throws<OverflowException>(() => state.AddVariable("score", -1));
            Assert.That(state.GetVariable("score"), Is.EqualTo(int.MinValue));
        }

        [Test]
        public void NormalizeRepairsMissingCollectionsAndClampsPlayerStats()
        {
            var state = new GameState
            {
                switches = null, selfSwitches = null, variables = null,
                player = new PlayerStats { maxHealth = 0, health = 90, maxSanity = -1, sanity = -10, gold = -1 }
            };
            state.inventory.Add(new IntEntry { key = "key", value = -8 });
            state.inventory.Add(null);
            state.Normalize();
            Assert.That(state.player.maxHealth, Is.EqualTo(1));
            Assert.That(state.player.health, Is.EqualTo(1));
            Assert.That(state.player.maxSanity, Is.EqualTo(1));
            Assert.That(state.player.sanity, Is.Zero);
            Assert.That(state.player.gold, Is.Zero);
            Assert.That(state.GetItemCount("key"), Is.Zero);
            Assert.That(state.switches, Is.Not.Null);
            Assert.That(state.selfSwitches, Is.Not.Null);
            Assert.That(state.variables, Is.Not.Null);
            state.player = null;
            state.inventory = null;
            state.Normalize();
            Assert.That(state.player, Is.Not.Null);
            Assert.That(state.inventory, Is.Not.Null);
        }

        [Test]
        public void ConditionsUseLocalEventStateAndInventory()
        {
            var state = new GameState();
            state.SetSwitch("night", true);
            state.SetSelfSwitch("npc:one", "A", true);
            state.SetVariable("chapter", 3);
            state.AddItem("key", 1);
            Assert.That(new StateCondition { kind = ConditionKind.Switch, key = "night" }.Evaluate(state, "npc:one"), Is.True);
            Assert.That(new StateCondition { kind = ConditionKind.Switch, key = "night", expected = false }.Evaluate(state, "npc:one"), Is.False);
            var self = new StateCondition { kind = ConditionKind.SelfSwitch, key = "A" };
            Assert.That(self.Evaluate(state, "npc:one"), Is.True);
            Assert.That(self.Evaluate(state, "npc:two"), Is.False);
            Assert.That(self.Evaluate(state, null), Is.False);
            Assert.That(new StateCondition { kind = ConditionKind.Variable, key = "chapter", value = 3 }.Evaluate(state, "npc:one"), Is.True);
            Assert.That(new StateCondition { kind = ConditionKind.Item, key = "key", value = 2 }.Evaluate(state, "npc:one"), Is.False);
            Assert.That(new StateCondition { kind = ConditionKind.Always }.Evaluate(null, null), Is.True);
            Assert.That(new StateCondition { kind = ConditionKind.Switch, key = "night" }.Evaluate(null, null), Is.False);
            Assert.That(new StateCondition { kind = ConditionKind.Switch, key = " " }.Evaluate(state, null), Is.False);
        }

        [TestCase(NumberComparison.Equal, 2, 2, true)]
        [TestCase(NumberComparison.Equal, 1, 2, false)]
        [TestCase(NumberComparison.NotEqual, 1, 2, true)]
        [TestCase(NumberComparison.NotEqual, 2, 2, false)]
        [TestCase(NumberComparison.Greater, 3, 2, true)]
        [TestCase(NumberComparison.Greater, 2, 2, false)]
        [TestCase(NumberComparison.GreaterOrEqual, 2, 2, true)]
        [TestCase(NumberComparison.GreaterOrEqual, 1, 2, false)]
        [TestCase(NumberComparison.Less, 1, 2, true)]
        [TestCase(NumberComparison.Less, 2, 2, false)]
        [TestCase(NumberComparison.LessOrEqual, 2, 2, true)]
        [TestCase(NumberComparison.LessOrEqual, 3, 2, false)]
        public void NumericConditionsRespectBoundary(NumberComparison comparison, int left, int right, bool expected)
        {
            Assert.That(StateCondition.Compare(left, right, comparison), Is.EqualTo(expected));
        }
    }
}
