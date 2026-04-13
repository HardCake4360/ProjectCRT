using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
[ExecuteAlways]
public class AffectPulseGraphic : MaskableGraphic
{
    [SerializeField, Range(-1f, 1f)] private float interest;
    [SerializeField, Range(-1f, 1f)] private float attitude;
    [SerializeField] private float markerRadius = 7f;
    [SerializeField] private float ringThickness = 2.5f;
    [SerializeField] private float ringCycleSeconds = 1.2f;
    [SerializeField] private float ringStartRadius = 12f;
    [SerializeField] private float ringEndRadius = 42f;
    [SerializeField] private float markerPadding = 14f;
    [SerializeField] private int circleSegments = 32;
    [SerializeField] private float updateFlashDuration = 0.45f;
    [SerializeField] private float updateFlashThickness = 3.5f;
    [SerializeField] private float updateFlashEndRadius = 58f;
    [SerializeField] private float colorReturnDuration = 0.45f;
    [SerializeField] private float minimumPendingSeconds = 1f;
    [SerializeField] private Color pendingColor = new Color(0.22f, 0.24f, 0.26f, 1f);

    private float updateFlashStartedAt = -1f;
    private float colorFlashTimer;
    private float lastUpdateTime;
    private float pendingStartedAt = -1f;
    private float queuedInterest;
    private float queuedAttitude;
    private bool pending;
    private bool hasInitialValue;
    private bool hasQueuedArrival;

    public void SetAffect(float nextInterest, float nextAttitude)
    {
        ApplyAffect(nextInterest, nextAttitude, true, true);
    }

    public void ResetAffect(float nextInterest, float nextAttitude)
    {
        ApplyAffect(nextInterest, nextAttitude, false, false);
    }

    public void SetPendingState(bool enabled)
    {
        pending = enabled;
        if (pending)
        {
            pendingStartedAt = GetClockSeconds();
            hasQueuedArrival = false;
            colorFlashTimer = 0f;
            updateFlashStartedAt = -1f;
        }
        else
        {
            pendingStartedAt = -1f;
            hasQueuedArrival = false;
        }

        SetVerticesDirty();
    }

    public void PreviewPendingState()
    {
        SetPendingState(true);
    }

    public void PreviewArrivalFlash()
    {
        ApplyAffect(interest, attitude, true, true);
    }

    public void PreviewResetState()
    {
        ResetAffect(interest, attitude);
    }

    private void ApplyAffect(float nextInterest, float nextAttitude, bool playArrivalFlash, bool respectMinimumPendingTime)
    {
        float clampedInterest = Mathf.Clamp(nextInterest, -1f, 1f);
        float clampedAttitude = Mathf.Clamp(nextAttitude, -1f, 1f);

        if (respectMinimumPendingTime && pending && playArrivalFlash && !HasMetMinimumPendingTime())
        {
            queuedInterest = clampedInterest;
            queuedAttitude = clampedAttitude;
            hasQueuedArrival = true;
            SetVerticesDirty();
            return;
        }

        bool changed = !hasInitialValue
            || !Mathf.Approximately(interest, clampedInterest)
            || !Mathf.Approximately(attitude, clampedAttitude);

        interest = clampedInterest;
        attitude = clampedAttitude;
        hasInitialValue = true;
        pending = false;
        pendingStartedAt = -1f;
        hasQueuedArrival = false;

        if (playArrivalFlash)
        {
            colorFlashTimer = Mathf.Max(0.01f, colorReturnDuration);
        }
        else
        {
            colorFlashTimer = 0f;
        }

        if (changed && playArrivalFlash)
        {
            updateFlashStartedAt = GetClockSeconds();
        }

        SetVerticesDirty();
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        markerRadius = Mathf.Max(1f, markerRadius);
        ringThickness = Mathf.Max(0.5f, ringThickness);
        ringCycleSeconds = Mathf.Max(0.1f, ringCycleSeconds);
        ringStartRadius = Mathf.Max(markerRadius + 2f, ringStartRadius);
        ringEndRadius = Mathf.Max(ringStartRadius + 4f, ringEndRadius);
        updateFlashDuration = Mathf.Max(0.05f, updateFlashDuration);
        updateFlashThickness = Mathf.Max(0.5f, updateFlashThickness);
        updateFlashEndRadius = Mathf.Max(ringStartRadius + 4f, updateFlashEndRadius);
        colorReturnDuration = Mathf.Max(0.05f, colorReturnDuration);
        minimumPendingSeconds = Mathf.Max(0f, minimumPendingSeconds);
        circleSegments = Mathf.Max(12, circleSegments);
        SetVerticesDirty();
    }

    private void Update()
    {
        float currentTime = GetClockSeconds();
        float deltaTime = lastUpdateTime > 0f
            ? Mathf.Max(0f, currentTime - lastUpdateTime)
            : Time.unscaledDeltaTime;
        lastUpdateTime = currentTime;

        if (pending && hasQueuedArrival && HasMetMinimumPendingTime())
        {
            ApplyAffect(queuedInterest, queuedAttitude, true, false);
        }

        if (colorFlashTimer > 0f)
        {
            colorFlashTimer = Mathf.Max(0f, colorFlashTimer - deltaTime);
        }

        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        Rect rect = rectTransform.rect;
        if (rect.width <= 0f || rect.height <= 0f)
        {
            return;
        }

        Vector2 center = EvaluateCenter(rect);
        Color signalColor = EvaluateSignalColor();

        AddFilledCircle(vh, center, markerRadius, signalColor);

        float currentTime = GetClockSeconds();
        float cycleT = Mathf.Repeat(currentTime / ringCycleSeconds, 1f);
        AddRing(vh, center, Mathf.Lerp(ringStartRadius, ringEndRadius, cycleT), ringThickness, WithExistingAlpha(signalColor));

        float cycleT2 = Mathf.Repeat((currentTime / ringCycleSeconds) + 0.5f, 1f);
        AddRing(vh, center, Mathf.Lerp(ringStartRadius, ringEndRadius, cycleT2), ringThickness, WithExistingAlpha(signalColor));

        if (updateFlashStartedAt >= 0f)
        {
            float flashT = Mathf.Clamp01((currentTime - updateFlashStartedAt) / updateFlashDuration);
            if (flashT < 1f)
            {
                float easedT = 1f - Mathf.Pow(1f - flashT, 3f);
                float flashRadius = Mathf.Lerp(markerRadius + 2f, updateFlashEndRadius, easedT);
                AddRing(vh, center, flashRadius, updateFlashThickness, WithExistingAlpha(Color.white));
            }
            else
            {
                updateFlashStartedAt = -1f;
            }
        }
    }

    private Vector2 EvaluateCenter(Rect rect)
    {
        float halfWidth = Mathf.Max(0f, (rect.width * 0.5f) - markerPadding);
        float halfHeight = Mathf.Max(0f, (rect.height * 0.5f) - markerPadding);
        return new Vector2(rect.center.x + (attitude * halfWidth), rect.center.y + (interest * halfHeight));
    }

    private Color EvaluateSignalColor()
    {
        if (pending)
        {
            return WithExistingAlpha(pendingColor);
        }

        float energy = Mathf.Clamp01((Mathf.Abs(interest) + Mathf.Abs(attitude)) * 0.5f);
        Color signalColor = Color.Lerp(new Color(0.45f, 0.88f, 0.83f), new Color(1f, 0.78f, 0.32f), energy);

        if (colorFlashTimer > 0f)
        {
            float flashLerp = colorReturnDuration <= 0f
                ? 1f
                : 1f - (colorFlashTimer / colorReturnDuration);
            signalColor = Color.Lerp(Color.white, signalColor, Mathf.Clamp01(flashLerp));
        }

        return WithExistingAlpha(signalColor);
    }

    private Color WithExistingAlpha(Color sourceRgb)
    {
        sourceRgb.a = color.a;
        return sourceRgb;
    }

    private float GetClockSeconds()
    {
        return Time.realtimeSinceStartup;
    }

    private bool HasMetMinimumPendingTime()
    {
        if (minimumPendingSeconds <= 0f || pendingStartedAt < 0f)
        {
            return true;
        }

        return GetClockSeconds() - pendingStartedAt >= minimumPendingSeconds;
    }

    private void AddFilledCircle(VertexHelper vh, Vector2 center, float radius, Color drawColor)
    {
        int startIndex = vh.currentVertCount;
        UIVertex vertex = UIVertex.simpleVert;
        vertex.color = drawColor;
        vertex.position = center;
        vh.AddVert(vertex);

        for (int i = 0; i <= circleSegments; i++)
        {
            float angle = (Mathf.PI * 2f * i) / circleSegments;
            Vector2 point = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            vertex.position = point;
            vh.AddVert(vertex);

            if (i > 0)
            {
                vh.AddTriangle(startIndex, startIndex + i, startIndex + i + 1);
            }
        }
    }

    private void AddRing(VertexHelper vh, Vector2 center, float radius, float thickness, Color drawColor)
    {
        int startIndex = vh.currentVertCount;
        float innerRadius = Mathf.Max(0.1f, radius - (thickness * 0.5f));
        float outerRadius = radius + (thickness * 0.5f);
        UIVertex vertex = UIVertex.simpleVert;
        vertex.color = drawColor;

        for (int i = 0; i <= circleSegments; i++)
        {
            float angle = (Mathf.PI * 2f * i) / circleSegments;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

            vertex.position = center + (direction * outerRadius);
            vh.AddVert(vertex);
            vertex.position = center + (direction * innerRadius);
            vh.AddVert(vertex);

            if (i > 0)
            {
                int index = startIndex + (i * 2);
                vh.AddTriangle(index - 2, index, index - 1);
                vh.AddTriangle(index, index + 1, index - 1);
            }
        }
    }
}
