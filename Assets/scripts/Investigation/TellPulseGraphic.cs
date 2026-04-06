using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public class TellPulseGraphic : MaskableGraphic
{
    [Header("Signal")]
    [SerializeField, Range(0f, 1f)] private float tellNormalized;
    [SerializeField] private float minPulseBpm = 48f;
    [SerializeField] private float maxPulseBpm = 132f;

    [Header("Vertices")]
    [SerializeField, Min(4)] private int vertexCount = 12;
    [SerializeField] private float lineThickness = 3f;
    [SerializeField] private float glowThickness = 8f;
    [SerializeField, Range(0.05f, 0.95f)] private float maxAmplitudeNormalized = 0.42f;
    [SerializeField, Range(0.2f, 3f)] private float centerWeightPower = 1.2f;

    [Header("Pulse Shape")]
    [SerializeField, Range(0.08f, 0.9f)] private float pulseDurationSeconds = 0.26f;
    [SerializeField, Range(0f, 1f)] private float pulseIrregularity = 0.15f;
    [SerializeField, Range(0f, 1f)] private float beatIntervalJitter = 0.12f;
    [SerializeField, Range(0f, 1f)] private float beatAmplitudeJitter = 0.1f;

    [Header("Pending Noise")]
    [SerializeField, Range(0.02f, 0.8f)] private float pendingNoiseAmplitudeNormalized = 0.26f;
    [SerializeField, Range(0.5f, 12f)] private float pendingNoiseSpeed = 5.5f;

    private struct ActivePulse
    {
        public float elapsed;
        public float durationMultiplier;
        public float amplitudeMultiplier;
    }

    private readonly List<Vector2> vertices = new List<Vector2>();
    private readonly List<ActivePulse> activePulses = new List<ActivePulse>();
    private float nextPulseTimer;
    private float pulseSeed;
    private bool pendingNoise;

    public float MinPulseBpm
    {
        get => minPulseBpm;
        set => minPulseBpm = Mathf.Max(1f, value);
    }

    public float MaxPulseBpm
    {
        get => maxPulseBpm;
        set => maxPulseBpm = Mathf.Max(minPulseBpm, value);
    }

    public float PulseDurationSeconds
    {
        get => pulseDurationSeconds;
        set => pulseDurationSeconds = Mathf.Max(0.08f, value);
    }

    public void SetTellNormalized(float value)
    {
        tellNormalized = Mathf.Clamp01(value);
    }

    public void SetPendingNoise(bool enabled)
    {
        pendingNoise = enabled;
        if (pendingNoise)
        {
            activePulses.Clear();
        }
        else
        {
            nextPulseTimer = GetBeatPeriodSeconds();
        }

        RebuildVertices();
        SetVerticesDirty();
    }

    public void ResetSignal(float initialTell = 0f)
    {
        tellNormalized = Mathf.Clamp01(initialTell);
        pendingNoise = false;
        activePulses.Clear();
        pulseSeed = 0f;
        nextPulseTimer = GetBeatPeriodSeconds();
        RebuildVertices();
        SetVerticesDirty();
    }

    protected override void Awake()
    {
        base.Awake();
        ResetSignal(tellNormalized);
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        if (vertices.Count == 0)
        {
            ResetSignal(tellNormalized);
        }
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        vertexCount = Mathf.Max(4, vertexCount);
        lineThickness = Mathf.Max(1f, lineThickness);
        glowThickness = Mathf.Max(lineThickness, glowThickness);
        minPulseBpm = Mathf.Max(1f, minPulseBpm);
        maxPulseBpm = Mathf.Max(minPulseBpm, maxPulseBpm);
        pulseDurationSeconds = Mathf.Max(0.08f, pulseDurationSeconds);
        pulseIrregularity = Mathf.Clamp01(pulseIrregularity);
        beatIntervalJitter = Mathf.Clamp01(beatIntervalJitter);
        beatAmplitudeJitter = Mathf.Clamp01(beatAmplitudeJitter);
        RebuildVertices();
        SetVerticesDirty();
    }

    private void Update()
    {
        float deltaTime = Time.unscaledDeltaTime;

        if (pendingNoise)
        {
            RebuildVertices();
            SetVerticesDirty();
            return;
        }

        nextPulseTimer -= deltaTime;

        for (int i = activePulses.Count - 1; i >= 0; i--)
        {
            ActivePulse pulse = activePulses[i];
            pulse.elapsed += deltaTime;

            if (pulse.elapsed >= GetPulseDuration(pulse))
            {
                activePulses.RemoveAt(i);
                continue;
            }

            activePulses[i] = pulse;
        }

        while (nextPulseTimer <= 0f)
        {
            BeginPulse();
            nextPulseTimer += GetBeatPeriodSeconds();
        }

        RebuildVertices();
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        if (vertices.Count < 2)
        {
            return;
        }

        Color glowColor = color * new Color(1f, 1f, 1f, 0.2f);
        DrawPolyline(vh, glowThickness, glowColor);
        DrawPolyline(vh, lineThickness, color);
    }

    private void DrawPolyline(VertexHelper vh, float thickness, Color drawColor)
    {
        int baseIndex = vh.currentVertCount;

        for (int i = 0; i < vertices.Count - 1; i++)
        {
            Vector2 a = vertices[i];
            Vector2 b = vertices[i + 1];

            Vector2 direction = (b - a).normalized;
            if (direction.sqrMagnitude <= Mathf.Epsilon)
            {
                direction = Vector2.right;
            }

            Vector2 normal = new Vector2(-direction.y, direction.x);
            Vector2 offset = normal * (thickness * 0.5f);

            UIVertex vertex = UIVertex.simpleVert;
            vertex.color = drawColor;

            vertex.position = a - offset;
            vh.AddVert(vertex);
            vertex.position = a + offset;
            vh.AddVert(vertex);
            vertex.position = b + offset;
            vh.AddVert(vertex);
            vertex.position = b - offset;
            vh.AddVert(vertex);

            int vertexIndex = baseIndex + (i * 4);
            vh.AddTriangle(vertexIndex, vertexIndex + 1, vertexIndex + 2);
            vh.AddTriangle(vertexIndex, vertexIndex + 2, vertexIndex + 3);
        }
    }

    private void RebuildVertices()
    {
        vertices.Clear();

        Rect rect = rectTransform.rect;
        if (rect.width <= 0f || rect.height <= 0f)
        {
            return;
        }

        float startX = rect.xMin;
        float stepX = rect.width / (vertexCount - 1);
        float centerY = rect.center.y;
        float pulseOffsetScale = EvaluatePulseOffsetScale();
        float maxAmplitudePixels = rect.height * maxAmplitudeNormalized;
        int middleIndex = vertexCount - 1;

        for (int i = 0; i < vertexCount; i++)
        {
            float x = startX + (stepX * i);
            float y = centerY;

            bool isEdge = i == 0 || i == vertexCount - 1;
            if (!isEdge)
            {
                float normalizedDistanceFromCenter = Mathf.Abs(((i / (float)middleIndex) - 0.5f) / 0.5f);
                float centerWeight = Mathf.Pow(1f - Mathf.Clamp01(normalizedDistanceFromCenter), centerWeightPower);

                if (pendingNoise)
                {
                    float noiseAmplitudePixels = rect.height * pendingNoiseAmplitudeNormalized;
                    float noiseTime = Time.unscaledTime * pendingNoiseSpeed;
                    float noiseSample = (Mathf.PerlinNoise((i * 0.41f) + noiseTime, 0.23f) - 0.5f) * 2f;
                    float secondaryNoise = (Mathf.PerlinNoise((i * 0.19f) + 0.77f, noiseTime * 0.83f) - 0.5f) * 2f;
                    y += (noiseSample * 0.75f + secondaryNoise * 0.25f) * noiseAmplitudePixels * Mathf.Lerp(0.6f, 1f, centerWeight);
                }
                else
                {
                    float direction = (i % 2 == 1) ? 1f : -1f;
                    y += direction * maxAmplitudePixels * centerWeight * pulseOffsetScale;
                }
            }

            vertices.Add(new Vector2(x, y));
        }
    }

    private void BeginPulse()
    {
        pulseSeed += 1f;

        float irregularityStrength = pulseIrregularity * Mathf.Lerp(0.8f, 1.1f, tellNormalized);
        float amplitudeNoise = (Mathf.PerlinNoise((pulseSeed * 0.73f) + 0.17f, 0.31f) - 0.5f) * 2f;
        float durationNoise = (Mathf.PerlinNoise((pulseSeed * 0.47f) + 1.37f, 0.21f) - 0.5f) * 2f;

        ActivePulse pulse = new ActivePulse
        {
            elapsed = 0f,
            amplitudeMultiplier = Mathf.Clamp(1f + (amplitudeNoise * beatAmplitudeJitter * irregularityStrength), 0.75f, 1.35f),
            durationMultiplier = Mathf.Clamp(1f + (durationNoise * beatIntervalJitter * irregularityStrength * 0.5f), 0.82f, 1.18f)
        };

        activePulses.Add(pulse);
    }

    private float EvaluatePulseOffsetScale()
    {
        if (activePulses.Count == 0)
        {
            return 0f;
        }

        float combined = 0f;

        for (int i = 0; i < activePulses.Count; i++)
        {
            ActivePulse pulse = activePulses[i];
            float duration = GetPulseDuration(pulse);
            float t = Mathf.Clamp01(pulse.elapsed / Mathf.Max(0.001f, duration));
            combined += (1f - EaseOutCubic(t)) * pulse.amplitudeMultiplier;
        }

        return Mathf.Clamp(combined, 0f, 2f);
    }

    private float GetPulseDuration(ActivePulse pulse)
    {
        return pulseDurationSeconds * pulse.durationMultiplier;
    }

    private float GetBeatPeriodSeconds()
    {
        float bpm = Mathf.Lerp(minPulseBpm, maxPulseBpm, tellNormalized);
        float interval = 60f / Mathf.Max(1f, bpm);
        float jitterNoise = (Mathf.PerlinNoise((pulseSeed * 0.29f) + 0.11f, tellNormalized * 4.7f) - 0.5f) * 2f;
        float jitterStrength = beatIntervalJitter * pulseIrregularity * Mathf.Lerp(0.8f, 1.1f, tellNormalized);
        return Mathf.Max(0.12f, interval * (1f + (jitterNoise * jitterStrength)));
    }

    private float EaseOutCubic(float t)
    {
        t = Mathf.Clamp01(t);
        float inverted = 1f - t;
        return 1f - (inverted * inverted * inverted);
    }
}
