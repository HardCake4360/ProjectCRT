using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace NoSleep.Tests
{
    public sealed class NpcContactEdgeTests
    {
        GameObject host;
        GameSession session;
        EventRunner runner;
        EventDefinition touch, busy;
        EventObject owner;
        Collider player;
        [SetUp]
        public void Setup()
        {
            host = new GameObject("Moving contact edge test");
            var stateHost = new GameObject("Isolated state"); stateHost.SetActive(false); stateHost.transform.SetParent(host.transform);
            session = stateHost.AddComponent<GameSession>(); runner = host.AddComponent<EventRunner>(); runner.Configure(session, null);
            typeof(GameSession).GetProperty(nameof(GameSession.Events)).GetSetMethod(true).Invoke(session, new object[] { runner });
            touch = ScriptableObject.CreateInstance<EventDefinition>(); touch.pages.Add(new EventPage { trigger = EventTrigger.PlayerTouch,
                commands = new List<EventCommand> { new EventCommand { type = EventCommandType.AddVariable, key = "contacts", intValue = 1 } } }); touch.EnsureStableIds();
            busy = ScriptableObject.CreateInstance<EventDefinition>(); busy.pages.Add(new EventPage { commands = new List<EventCommand> { new EventCommand { type = EventCommandType.Wait, seconds = 60 } } }); busy.EnsureStableIds();
            var eventHost = new GameObject("Moving contact"); eventHost.transform.SetParent(host.transform); owner = eventHost.AddComponent<EventObject>(); owner.Configure(session, touch, "test/contact");
            var playerHost = new GameObject("Player"); playerHost.transform.SetParent(host.transform); playerHost.AddComponent<PlayerController>(); player = playerHost.GetComponent<CharacterController>();
        }
        [UnityTearDown]
        public IEnumerator Cleanup()
        { runner.Cancel(); Object.Destroy(host); Object.Destroy(touch); Object.Destroy(busy); yield return null; }

        [UnityTest]
        public IEnumerator PhysicsAndMovingNpcProximityShareOneEntryUntilBothExit()
        {
            owner.ObserveNpcContact(true); Assert.That(session.State.GetVariable("contacts"), Is.EqualTo(1));
            owner.SendMessage("OnTriggerEnter", player); owner.SendMessage("OnTriggerStay", player); owner.ObserveNpcContact(true);
            Assert.That(session.State.GetVariable("contacts"), Is.EqualTo(1));
            owner.ObserveNpcContact(false); owner.SendMessage("OnTriggerStay", player);
            Assert.That(session.State.GetVariable("contacts"), Is.EqualTo(1), "Leaving only the proximity radius must not rearm a physics overlap.");
            owner.SendMessage("OnTriggerExit", player); owner.ObserveNpcContact(true);
            Assert.That(session.State.GetVariable("contacts"), Is.EqualTo(2)); yield return null;
        }

        [UnityTest]
        public IEnumerator BusyEntryWaitsOnceAndCancelledForegroundDoesNotLoseTheContact()
        {
            Assert.That(runner.TryRun(busy, "test/busy"), Is.True);
            owner.ObserveNpcContact(true); owner.SendMessage("OnTriggerEnter", player); yield return null;
            Assert.That(session.State.GetVariable("contacts"), Is.Zero);
            runner.Cancel(); owner.ObserveNpcContact(true); owner.SendMessage("OnTriggerStay", player);
            Assert.That(session.State.GetVariable("contacts"), Is.EqualTo(1));
            owner.ObserveNpcContact(true); owner.SendMessage("OnTriggerStay", player);
            Assert.That(session.State.GetVariable("contacts"), Is.EqualTo(1)); yield return null;
        }
    }
}
