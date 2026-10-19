using UnityEngine;

namespace NoSleep
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerController : MonoBehaviour
    {
        public float speed = 3;
        public float runMultiplier = 1.65f;
        public float interactionRange = 1.7f;
        public float Radius => body != null ? body.radius : 0.23f;
        public EventObject InteractionTarget { get; private set; }
        GameSession session;
        CharacterController body;
        readonly Collider[] hits = new Collider[32];
        float verticalSpeed;

        void Awake() { body = GetComponent<CharacterController>(); }
        public void Configure(GameSession game) { session = game; body = GetComponent<CharacterController>(); }
        void Update()
        {
            if (session == null || !session.IsReady) return;
            if (session.IsInputBlocked) { InteractionTarget = null; return; }
            var input = GameInput.Move;
            var camera = Camera.main;
            Vector3 forward = camera != null ? Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up).normalized : Vector3.forward;
            Vector3 right = camera != null ? Vector3.ProjectOnPlane(camera.transform.right, Vector3.up).normalized : Vector3.right;
            Vector3 direction = forward * input.y + right * input.x;
            float fatigueSpeed = session.HorrorEnabled ? SurvivalRules.MovementMultiplier(session.State, session.SurvivalSettings) : 1;
            var displacement = direction * (speed * fatigueSpeed * (GameInput.Run ? runMultiplier : 1) * Time.deltaTime);
            // Substeps keep thin blocked cells from being crossed during slow frames.
            int steps = Mathf.Clamp(Mathf.CeilToInt(displacement.magnitude / Mathf.Max(0.05f, session.Map.Map.cellSize * 0.2f)), 1, 64);
            Vector3 step = displacement / steps;
            for (int i = 0; i < steps; i++)
            {
                var current = transform.position;
                if (session.Map.CanOccupy(current + step, Radius)) body.Move(step);
                else
                {
                    var x = new Vector3(step.x, 0, 0); var z = new Vector3(0, 0, step.z);
                    if (session.Map.CanOccupy(transform.position + x, Radius)) body.Move(x);
                    if (session.Map.CanOccupy(transform.position + z, Radius)) body.Move(z);
                }
            }
            if (direction.sqrMagnitude > 0.01f) transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), 720 * Time.deltaTime);
            verticalSpeed = body.isGrounded ? -2 : Mathf.Max(-20, verticalSpeed - 25 * Time.deltaTime);
            body.Move(Vector3.up * (verticalSpeed * Time.deltaTime));
            FindInteraction();
            if (GameInput.Confirm && InteractionTarget != null) InteractionTarget.TryInteract();
        }
        void FindInteraction()
        {
            InteractionTarget = null; float nearest = interactionRange * interactionRange;
            int count = Physics.OverlapSphereNonAlloc(transform.position + Vector3.up * 0.5f, interactionRange, hits, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                var candidate = hits[i].GetComponentInParent<EventObject>();
                if (candidate == null || !candidate.CanInteract) continue;
                var delta = candidate.transform.position - transform.position; delta.y = 0;
                float distance = delta.sqrMagnitude;
                if (distance >= nearest) continue;
                // Solid walls prevent interactions through adjacent rooms; the target's own collider is allowed.
                var origin = transform.position + Vector3.up * 0.7f;
                var target = candidate.transform.position + Vector3.up * 0.7f;
                if (Physics.Linecast(origin, target, out var obstruction, ~0, QueryTriggerInteraction.Ignore)
                    && obstruction.collider.GetComponentInParent<EventObject>() != candidate
                    && obstruction.collider.GetComponentInParent<PlayerController>() != this) continue;
                nearest = distance; InteractionTarget = candidate;
            }
        }
        public void Warp(Vector3 position, float yaw = 0)
        {
            if (body == null) body = GetComponent<CharacterController>();
            body.enabled = false; transform.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));
            verticalSpeed = 0; InteractionTarget = null; body.enabled = true;
        }
    }
}
