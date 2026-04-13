using UnityEngine;

[RequireComponent(typeof(Animator))]
public class NpcLookAtIKDriver : MonoBehaviour
{
    [SerializeField] private Animator targetAnimator;

    private Transform lookAtTarget;
    private bool lookAtEnabled;
    private float lookAtWeight = 1f;
    private float bodyWeight = 0.15f;
    private float headWeight = 0.85f;
    private float eyesWeight = 0.4f;
    private float clampWeight = 0.7f;
    private float blendSpeed = 6f;
    private Vector3 targetOffset = new Vector3(0f, -0.05f, 0f);
    private float currentWeight;

    private void Awake()
    {
        if (targetAnimator == null)
        {
            targetAnimator = GetComponent<Animator>();
        }
    }

    public void Configure(float weight, float body, float head, float eyes, float clamp, float blend, Vector3 offset)
    {
        lookAtWeight = Mathf.Clamp01(weight);
        bodyWeight = Mathf.Clamp01(body);
        headWeight = Mathf.Clamp01(head);
        eyesWeight = Mathf.Clamp01(eyes);
        clampWeight = Mathf.Clamp01(clamp);
        blendSpeed = Mathf.Max(0.01f, blend);
        targetOffset = offset;
    }

    public void SetLookAtTarget(Transform target, bool enabled)
    {
        lookAtTarget = target;
        lookAtEnabled = enabled && lookAtTarget != null;
    }

    private void OnAnimatorIK(int layerIndex)
    {
        if (targetAnimator == null)
        {
            return;
        }

        float targetWeight = lookAtEnabled && lookAtTarget != null ? lookAtWeight : 0f;
        currentWeight = Mathf.MoveTowards(
            currentWeight,
            targetWeight,
            Mathf.Max(0.001f, blendSpeed) * Time.deltaTime);

        targetAnimator.SetLookAtWeight(currentWeight, bodyWeight, headWeight, eyesWeight, clampWeight);

        if (currentWeight > 0f && lookAtTarget != null)
        {
            targetAnimator.SetLookAtPosition(lookAtTarget.position + targetOffset);
        }
    }
}
