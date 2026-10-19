using UnityEngine;

namespace NoSleep
{
    [RequireComponent(typeof(Camera))]
    public sealed class QuarterViewCamera : MonoBehaviour
    {
        public Transform target;
        public float yaw = 45;
        [Range(15, 85)] public float pitch = 50;
        public float distance = 16;
        public float orthographicSize = 7;
        public bool orthographic = true;
        public float followSmoothTime = 0.12f;
        Vector3 velocity;
        public void Snap() { velocity = Vector3.zero; Apply(true); }
        void LateUpdate() => Apply(false);
        void Apply(bool snap)
        {
            if (target == null) return;
            var camera = GetComponent<Camera>(); camera.orthographic = orthographic; camera.orthographicSize = orthographicSize;
            var rotation = Quaternion.Euler(pitch, yaw, 0);
            var position = target.position + Vector3.up * 0.65f - rotation * Vector3.forward * distance;
            transform.position = snap ? position : Vector3.SmoothDamp(transform.position, position, ref velocity, followSmoothTime);
            transform.rotation = rotation;
        }
    }
}
