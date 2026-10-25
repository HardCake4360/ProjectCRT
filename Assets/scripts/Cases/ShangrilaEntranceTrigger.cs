using UnityEngine;

namespace ProjectCRT.Cases
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class ShangrilaEntranceTrigger : MonoBehaviour
    {
        public ShangrilaCaseManager manager;
        bool accepted;
        void Update()
        {
            // Disabling a CharacterController for pose restoration can omit OnTriggerExit.
            // Compare collider bounds so the player's capsule overlap counts as still inside.
            if (!accepted || manager == null || manager.Player == null) return;
            var controller = manager.Player.GetComponent<CharacterController>();
            if (controller == null || !GetComponent<BoxCollider>().bounds.Intersects(controller.bounds)) accepted = false;
        }
        void OnTriggerEnter(Collider other) => TryEnter(other);
        void OnTriggerStay(Collider other) => TryEnter(other);
        void TryEnter(Collider other)
        {
            if (accepted || manager == null || other.GetComponentInParent<PlayerControler>() != manager.Player) return;
            accepted = manager.TryStartIntroduction();
        }
        void OnTriggerExit(Collider other)
        {
            if (manager != null && other.GetComponentInParent<PlayerControler>() == manager.Player) accepted = false;
        }
        void OnDisable() { accepted = false; }
    }
}
