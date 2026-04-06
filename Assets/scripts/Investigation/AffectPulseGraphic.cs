using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
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

    private float updateFlashStartedAt = -1f;
    private bool hasInitialValue;

    public void SetAffect(float nextInterest, float nextAttitude)
    {
        float clampedInterest = Mathf.Clamp(nextInterest, -1f, 1f);
        float clampedAttitude = Mathf.Clamp(nextAttitude, -1f, 1f);
        bool changed = !hasInitialValue
            || !Mathf.Approximately(interest, clampedInterest)
            || !Mathf.Approximately(attitude, clampedAttitude);

        interest = clampedInterest;
        attitude = clampedAttitude;
        hasInitialValue = true;

        if (changed)
        {
            updateFlashStartedAt = Time.unscaledTime;
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
        circleSegments = Mathf.Max(12, circleSegments);
        SetVerticesDirty();
    }

    private void Update()
    {
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

        float cycleT = Mathf.Repeat(Time.unscaledTime / ringCycleSeconds, 1f);
        AddRing(vh, center, Mathf.Lerp(ringStartRadius, ringEndRadius, cycleT), ringThickness, new Color(signalColor.r, signalColor.g, signalColor.b, Mathf.Lerp(0.45f, 0f, cycleT)));

        float cycleT2 = Mathf.Repeat((Time.unscaledTime / ringCycleSeconds) + 0.5f, 1f);
        AddRing(vh, center, Mathf.Lerp(ringStartRadius, ringEndRadius, cycleT2), ringThickness, new Color(signalColor.r, signalColor.g, signalColor.b, Mathf.Lerp(0.28f, 0f, cycleT2)));

        if (updateFlashStartedAt >= 0f)
        {
            float flashT = Mathf.Clamp01((Time.unscaledTime - updateFlashStartedAt) / updateFlashDuration);
            if (flashT < 1f)
            {
                float easedT = 1f - Mathf.Pow(1f - flashT, 3f);
                float flashRadius = Mathf.Lerp(markerRadius + 2f, updateFlashEndRadius, easedT);
                float flashAlpha = Mathf.Lerp(0.95f, 0f, flashT);
                AddRing(vh, center, flashRadius, updateFlashThickness, new Color(1f, 1f, 1f, flashAlpha));
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
        float energy = Mathf.Clamp01((Mathf.Abs(interest) + Mathf.Abs(attitude)) * 0.5f);
        return Color.Lerp(new Color(0.45f, 0.88f, 0.83f, 1f), new Color(1f, 0.78f, 0.32f, 1f), energy);
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
