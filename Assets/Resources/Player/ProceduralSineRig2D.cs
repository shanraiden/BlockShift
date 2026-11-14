using UnityEngine;

public class ProceduralSineRig2D : MonoBehaviour
{
    public enum TailMotionPattern
    {
        ClassicSine,
        DualHarmonic,
        WhipFollowThrough,
        LazyCurl,
        OrganicNoiseBlend,
        FigureEightFlutter
    }

    [Header("Torso & Root Breathing/Bounce")]
    public Transform rootBone;
    public float bounceAmplitude = 0.06f;
    public float bounceFrequency = 3.5f;

    [Header("FK Bone Chains (Tail & Wispy Ears)")]
    public Transform[] tailBones;      // Assign from base to tip
    public TailMotionPattern motionPattern = TailMotionPattern.DualHarmonic;
    public float tailWaveSpeed = 4.0f;
    public float tailBaseAngle = 8.0f; // Degrees of sway at base
    public float tailPhaseOffset = 0.6f; // Radian delay per bone segment

    [Header("Chain Taper & Curl Profile")]
    [Tooltip("How much amplitude grows from base to tip (e.g., 1.5 = +150% at the tip).")]
    public float tipAmplitudeGrowth = 1.0f;
    [Tooltip("Exponent controlling how amplitude scales toward the tip (1 = linear, >1 = whip-heavy tip).")]
    [Range(0.25f, 3f)]
    public float tipCurveExponent = 1.35f;
    [Tooltip("Constant curl angle bias per bone (positive curls one way, negative the other).")]
    public float curlBiasAngle = 0f;
    [Tooltip("Dynamic breathing of the curl bias over time.")]
    public float curlPulseAmount = 2.0f;
    public float curlPulseSpeed = 1.5f;

    [Header("Secondary Harmonic Wave")]
    public bool enableSecondaryWave = true;
    [Tooltip("Frequency multiplier relative to tailWaveSpeed.")]
    public float secondarySpeedMultiplier = 2.15f;
    [Tooltip("Amplitude ratio relative to tailBaseAngle.")]
    [Range(0f, 1f)]
    public float secondaryAmplitudeRatio = 0.35f;
    public float secondaryPhaseOffset = 1.1f;

    [Header("Organic Perlin Noise")]
    public bool enableOrganicNoise = true;
    public float noiseFrequency = 1.2f;
    public float noiseAngleAmplitude = 4.0f;
    public float noiseBoneSpacing = 0.45f;

    [Header("Traveling Flick / Twitch Impulse")]
    public bool enableRandomFlicks = false;
    public Vector2 flickIntervalRange = new Vector2(3.0f, 7.0f);
    public float flickAngleStrength = 14.0f;
    public float flickTravelSpeed = 6.0f;
    public float flickDecaySpeed = 4.0f;

    [Header("Movement & Velocity Inertia")]
    public bool enableMovementInertia = true;
    [Tooltip("Optional Rigidbody2D to read velocity from. If null, uses transform world displacement.")]
    public Rigidbody2D referenceRigidbody;
    public float verticalInertiaStrength = 2.5f;
    public float horizontalInertiaStrength = 1.5f;
    [Tooltip("Maximum inertia bend angle in degrees. Automatically flips between +ve (Moving Left) and -ve (Moving Right).")]
    public float maxInertiaAngle = 18.0f;
    public float inertiaSmoothing = 8.0f;
    [Tooltip("If true, inverts the vertical tail lift direction during horizontal movement.")]
    public bool invertHorizontalInertia = false;

    [Header("Movement State Multipliers")]
    public bool isMoving = false;
    public bool isRunning = false;
    public float movingSpeedMultiplier = 1.35f;
    public float movingAngleMultiplier = 1.15f;
    public float runningSpeedMultiplier = 2.0f;
    public float runningAngleMultiplier = 1.4f;
    [Tooltip("Rotation interpolation speed (0 = instant).")]
    public float rotationSmoothingSpeed = 18.0f;

    private Vector3 initialRootLocalPos;
    private Quaternion[] initialTailRotations;
    private Quaternion[] currentTailRotations;

    private float wavePhaseTime;
    private float bouncePhaseTime;
    private Vector3 lastWorldPos;
    private Vector2 smoothedInertiaLag;

    // Flick state
    private float nextFlickTimer;
    private float flickProgress = -1f;
    private float currentFlickStrength;
    private float noiseSeed;

    public bool IsMoving
    {
        get => isMoving;
        set => isMoving = value;
    }

    public bool IsRunning
    {
        get => isRunning;
        set => isRunning = value;
    }

    void Start()
    {
        if (rootBone != null)
            initialRootLocalPos = rootBone.localPosition;

        if (tailBones != null)
        {
            initialTailRotations = new Quaternion[tailBones.Length];
            currentTailRotations = new Quaternion[tailBones.Length];
            for (int i = 0; i < tailBones.Length; i++)
            {
                if (tailBones[i] != null)
                {
                    initialTailRotations[i] = tailBones[i].localRotation;
                    currentTailRotations[i] = tailBones[i].localRotation;
                }
            }
        }

        lastWorldPos = transform.position;
        noiseSeed = Random.Range(0f, 100f);
        ScheduleNextFlick();
    }

    void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        float speedMult = isRunning ? runningSpeedMultiplier : (isMoving ? movingSpeedMultiplier : 1f);
        float angleMult = isRunning ? runningAngleMultiplier : (isMoving ? movingAngleMultiplier : 1f);

        wavePhaseTime += dt * tailWaveSpeed * speedMult;
        bouncePhaseTime += dt * bounceFrequency * speedMult;

        // 1. Vertical harmonic bounce on the torso/pelvis
        if (rootBone != null)
        {
            float yOffset = Mathf.Sin(bouncePhaseTime) * bounceAmplitude;
            rootBone.localPosition = initialRootLocalPos + new Vector3(0f, yOffset, 0f);
        }

        if (tailBones == null || tailBones.Length == 0)
            return;

        // 2. Update velocity inertia lag & random flick impulses
        UpdateMovementInertia(dt);
        UpdateFlickState(dt);

        int count = tailBones.Length;

        // 3. Evaluate complex wave motion down the tail/ear bone chain
        for (int i = 0; i < count; i++)
        {
            if (tailBones[i] == null) continue;

            // Normalized position along the tail [0 at base .. 1 at tip]
            float normalizedIndex = count > 1 ? i / (float)(count - 1) : 0f;
            float tipFactor = Mathf.Pow(normalizedIndex, tipCurveExponent);
            float tipMultiplier = 1f + tipFactor * tipAmplitudeGrowth;

            float primaryPhase = wavePhaseTime - (i * tailPhaseOffset);
            float angle = EvaluatePatternAngle(primaryPhase, i, normalizedIndex, tipFactor) * tailBaseAngle * tipMultiplier * angleMult;

            // Optional secondary harmonic layer
            if (enableSecondaryWave && motionPattern != TailMotionPattern.ClassicSine)
            {
                float secondaryPhase = (wavePhaseTime * secondarySpeedMultiplier) - (i * secondaryPhaseOffset);
                float secondaryAngle = Mathf.Sin(secondaryPhase) * (tailBaseAngle * secondaryAmplitudeRatio) * tipMultiplier * angleMult;
                angle += secondaryAngle;
            }

            // Optional organic Perlin noise layer
            if (enableOrganicNoise)
            {
                float noiseSample = Mathf.PerlinNoise(
                    noiseSeed + Time.time * noiseFrequency,
                    i * noiseBoneSpacing
                ) * 2f - 1f;
                angle += noiseSample * noiseAngleAmplitude * (0.5f + tipFactor);
            }

            // Curl bias + subtle breathing curl pulse
            float dynamicCurl = curlBiasAngle + Mathf.Sin(Time.time * curlPulseSpeed - i * 0.3f) * curlPulseAmount;
            angle += dynamicCurl * (0.4f + 0.6f * tipFactor);

            // Traveling flick pulse from base to tip
            if (flickProgress >= 0f)
            {
                float distFromPulse = Mathf.Abs(normalizedIndex - flickProgress);
                float pulseEnvelope = Mathf.Max(0f, 1f - distFromPulse * 3.0f);
                angle += pulseEnvelope * currentFlickStrength * tipMultiplier;
            }

            // Velocity/movement inertia drag
            if (enableMovementInertia)
            {
                float horizSign = invertHorizontalInertia ? -1f : 1f;
                // Use horizontal speed magnitude so local Z rotation bends the tail in the same vertical direction (Up) whether moving Left or Right
                float horizontalInertia = Mathf.Abs(smoothedInertiaLag.x) * horizontalInertiaStrength * horizSign;
                float verticalInertia = -smoothedInertiaLag.y * verticalInertiaStrength;

                float absMaxAngle = Mathf.Abs(maxInertiaAngle);
                float inertiaOffset = (verticalInertia + horizontalInertia) * (0.35f + 0.65f * tipFactor);
                inertiaOffset = Mathf.Clamp(inertiaOffset, -absMaxAngle, absMaxAngle);
                angle += inertiaOffset;
            }

            Quaternion targetRotation = initialTailRotations[i] * Quaternion.Euler(0f, 0f, angle);

            if (rotationSmoothingSpeed > 0f)
            {
                float lerpFactor = 1f - Mathf.Exp(-rotationSmoothingSpeed * dt);
                currentTailRotations[i] = Quaternion.Slerp(currentTailRotations[i], targetRotation, lerpFactor);
                tailBones[i].localRotation = currentTailRotations[i];
            }
            else
            {
                tailBones[i].localRotation = targetRotation;
            }
        }
    }

    private float EvaluatePatternAngle(float phase, int boneIndex, float normalizedIndex, float tipFactor)
    {
        switch (motionPattern)
        {
            case TailMotionPattern.ClassicSine:
                return Mathf.Sin(phase);

            case TailMotionPattern.DualHarmonic:
                return Mathf.Sin(phase) * 0.75f + Mathf.Cos(phase * 0.5f - boneIndex * 0.35f) * 0.25f;

            case TailMotionPattern.WhipFollowThrough:
                // Non-linear phase lag + sharpened wave crest toward the tip for whip snap
                float whipPhase = phase - (tipFactor * tipFactor * 1.25f);
                float rawSine = Mathf.Sin(whipPhase);
                return Mathf.Sign(rawSine) * Mathf.Pow(Mathf.Abs(rawSine), Mathf.Lerp(1f, 0.6f, tipFactor));

            case TailMotionPattern.LazyCurl:
                // Slow sweeping wave with asymmetric hold at the peak of the curl
                float s = Mathf.Sin(phase);
                return s + 0.35f * (s * s - 0.5f);

            case TailMotionPattern.OrganicNoiseBlend:
                // Softer carrier wave designed to blend with Perlin noise
                return Mathf.Sin(phase) * 0.6f + Mathf.Sin(phase * 1.618f + boneIndex) * 0.4f;

            case TailMotionPattern.FigureEightFlutter:
                // Slow base wave with rapid high-frequency flutter near the tip
                float baseWave = Mathf.Sin(phase * 0.7f);
                float tipFlutter = Mathf.Sin(phase * 2.8f) * tipFactor * 0.65f;
                return baseWave + tipFlutter;

            default:
                return Mathf.Sin(phase);
        }
    }

    private void UpdateMovementInertia(float dt)
    {
        if (!enableMovementInertia)
        {
            smoothedInertiaLag = Vector2.zero;
            lastWorldPos = transform.position;
            return;
        }

        Vector2 currentVelocity;
        if (referenceRigidbody != null)
        {
            currentVelocity = referenceRigidbody.linearVelocity;
        }
        else
        {
            currentVelocity = (Vector2)((transform.position - lastWorldPos) / Mathf.Max(dt, 0.0001f));
        }

        // Flip maxInertiaAngle between +ve (Moving Left) and -ve (Moving Right) and vice versa
        float absMaxAngle = Mathf.Abs(maxInertiaAngle);
        if (currentVelocity.x < -0.01f)
        {
            maxInertiaAngle = absMaxAngle;
        }
        else if (currentVelocity.x > 0.01f)
        {
            maxInertiaAngle = -absMaxAngle;
        }

        lastWorldPos = transform.position;
        float smoothFactor = 1f - Mathf.Exp(-inertiaSmoothing * dt);

        // Smooth horizontal speed magnitude so turning Left <-> Right does not dip the tail downward
        Vector2 targetInertiaVelocity = new Vector2(Mathf.Abs(currentVelocity.x), currentVelocity.y);
        smoothedInertiaLag = Vector2.Lerp(smoothedInertiaLag, targetInertiaVelocity, smoothFactor);
    }

    private void UpdateFlickState(float dt)
    {
        if (enableRandomFlicks)
        {
            nextFlickTimer -= dt;
            if (nextFlickTimer <= 0f)
            {
                float dir = Random.value > 0.5f ? 1f : -1f;
                TriggerTailFlick(dir);
                ScheduleNextFlick();
            }
        }

        if (flickProgress >= 0f)
        {
            flickProgress += dt * flickTravelSpeed;
            currentFlickStrength = Mathf.MoveTowards(currentFlickStrength, 0f, dt * flickDecaySpeed * Mathf.Abs(flickAngleStrength));

            if (flickProgress > 1.35f || Mathf.Abs(currentFlickStrength) <= 0.01f)
            {
                flickProgress = -1f;
            }
        }
    }

    private void ScheduleNextFlick()
    {
        float min = Mathf.Max(0.2f, Mathf.Min(flickIntervalRange.x, flickIntervalRange.y));
        float max = Mathf.Max(min, Mathf.Max(flickIntervalRange.x, flickIntervalRange.y));
        nextFlickTimer = Random.Range(min, max);
    }

    /// <summary>
    /// Manually triggers a traveling whip/flick impulse from the tail base to the tip.
    /// </summary>
    public void TriggerTailFlick(float directionSign = 1f)
    {
        flickProgress = 0f;
        currentFlickStrength = Mathf.Sign(directionSign) * flickAngleStrength;
    }
}