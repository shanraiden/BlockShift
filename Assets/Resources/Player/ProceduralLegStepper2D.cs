using System.Collections.Generic;
using UnityEngine;

public class ProceduralLegStepper2D : MonoBehaviour
{
    public enum SmoothingType
    {
        Smooth,
        Linear
    }

    public enum TimingOffsetMode
    {
        [Tooltip("Delays the leg by timingOffset (in seconds) and waits on the ground before taking the first step.")]
        TimeDelayWithStartWait,

        [Tooltip("Continuous time offset in seconds (e.g. 0.25s delay between legs) without startup wait.")]
        TimeOffsetSeconds,

        [Tooltip("Normalized cycle phase offset [0..1] (0.5 = 180 degrees opposite leg).")]
        NormalizedPhase
    }

    [System.Serializable]
    public struct StepParameters
    {
        [Tooltip("Full strides per second.")]
        public float cycleSpeed;

        [Tooltip("Horizontal radius of the step.")]
        public float strideLength;

        [Tooltip("Vertical peak of the semi-circle.")]
        public float stepHeight;

        [Tooltip("How the ikTarget interpolates along the path.")]
        public SmoothingType smoothingType;

        [Tooltip("Interpolation responsiveness when following the path (higher = tighter phase accuracy).")]
        public float smoothingSpeed;
    }

    [Header("References")]
    public Transform ikTarget;

    [Header("State Booleans")]
    [Tooltip("Enable walking/moving leg cycle.")]
    public bool isMoving = true;

    [Tooltip("Enable running leg cycle (takes priority over isMoving when both are checked).")]
    public bool isRunning = false;

    [Header("Shared Cycle & Timing Offset Settings")]
    [Tooltip("How timingOffset is interpreted.")]
    public TimingOffsetMode timingOffsetMode = TimingOffsetMode.TimeDelayWithStartWait;

    [Tooltip("Timing offset between legs. For alternating legs: use 0 on one leg and e.g. 0.25 (seconds) or 0.5 (normalized phase) on the other.")]
    [Range(0f, 4f)]
    public float timingOffset = 0f;

    [Tooltip("Optional pause duration (in seconds) at the end of each step before starting the next stride.")]
    [Range(0f, 2f)]
    public float stepRestDelay = 0f;

    public bool invertDirection = false; // Flip if character faces left

    [Header("Moving Parameters (When IsMoving is True)")]
    public StepParameters movingParameters = new StepParameters
    {
        cycleSpeed = 2.0f,
        strideLength = 0.5f,
        stepHeight = 0.35f,
        smoothingType = SmoothingType.Smooth,
        smoothingSpeed = 15f
    };

    [Header("Running Parameters (When IsRunning is True)")]
    public StepParameters runningParameters = new StepParameters
    {
        cycleSpeed = 3.5f,
        strideLength = 0.8f,
        stepHeight = 0.55f,
        smoothingType = SmoothingType.Linear,
        smoothingSpeed = 25f
    };

    [Header("Idle / Return Settings")]
    public SmoothingType idleSmoothingType = SmoothingType.Smooth;
    public float idleReturnSpeed = 10f;

    // Shared clock per character root so all legs on the same rig stay 100% phase-synchronized
    private class SharedRigClock
    {
        public int lastUpdatedFrame = -1;
        public float elapsedActiveTime = 0f;
        public float accumulatedCycles = 0f;
        public bool anyLegActiveLastFrame = false;
    }

    private static readonly Dictionary<Transform, SharedRigClock> s_rigClocks = new Dictionary<Transform, SharedRigClock>();

    private Vector3 initialLocalPos;
    private Transform rigRoot;
    private bool wasActiveLastFrame;
    private float legActiveElapsed;

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
        if (ikTarget == null)
            ikTarget = transform;

        // Store the resting position relative to the parent (creature body)
        initialLocalPos = ikTarget.localPosition;
        rigRoot = transform.root != null ? transform.root : transform;
    }

    void Update()
    {
        bool isActive = isMoving || isRunning;

        // When neither Moving nor Running is active, return ikTarget to resting position
        if (!isActive)
        {
            wasActiveLastFrame = false;
            legActiveElapsed = 0f;
            ApplyPositionSmoothing(initialLocalPos, idleSmoothingType, idleReturnSpeed, 1f);
            return;
        }

        // Select the active parameter set (Running takes priority if both are true)
        StepParameters activeParams = isRunning ? runningParameters : movingParameters;
        float speed = Mathf.Max(0.001f, activeParams.cycleSpeed);

        if (!wasActiveLastFrame)
        {
            wasActiveLastFrame = true;
            legActiveElapsed = 0f;
        }

        legActiveElapsed += Time.deltaTime;

        // Update shared rig clock once per frame per character root so all legs stay locked in sync
        SharedRigClock clock = GetAndUpdateSharedClock(speed);

        // If using startup wait, hold the leg planted at initialLocalPos until timingOffset has elapsed
        if (timingOffsetMode == TimingOffsetMode.TimeDelayWithStartWait && legActiveElapsed < timingOffset)
        {
            ApplyPositionSmoothing(initialLocalPos, activeParams.smoothingType, activeParams.smoothingSpeed, speed);
            return;
        }

        // Calculate the leg's effective cycle phase based on the selected TimingOffsetMode
        float normalizedPhase = CalculateNormalizedPhase(clock, speed);
        float dir = invertDirection ? -1f : 1f;

        float xOffset;
        float yOffset;

        if (activeParams.smoothingType == SmoothingType.Linear)
        {
            // Linear trajectory: constant horizontal speed during swing [0..0.5) and stance [0.5..1)
            if (normalizedPhase < 0.5f)
            {
                // Swing phase (0 -> 1): foot lifts and moves forward linearly
                float t = normalizedPhase * 2f;
                xOffset = Mathf.Lerp(-activeParams.strideLength, activeParams.strideLength, t) * dir;
                float lift = 1f - Mathf.Abs(t * 2f - 1f);
                yOffset = lift * activeParams.stepHeight;
            }
            else
            {
                // Stance phase (0 -> 1): foot stays flat on ground and sweeps back linearly
                float t = (normalizedPhase - 0.5f) * 2f;
                xOffset = Mathf.Lerp(activeParams.strideLength, -activeParams.strideLength, t) * dir;
                yOffset = 0f;
            }
        }
        else
        {
            // Smooth trajectory: sinusoidal ease-in/out sweep and semi-circle arc
            float phaseRadians = normalizedPhase * Mathf.PI * 2f;
            xOffset = -Mathf.Cos(phaseRadians) * activeParams.strideLength * dir;
            yOffset = Mathf.Max(0f, Mathf.Sin(phaseRadians)) * activeParams.stepHeight;
        }

        Vector3 targetLocalPos = initialLocalPos + new Vector3(xOffset, yOffset, 0f);
        ApplyPositionSmoothing(targetLocalPos, activeParams.smoothingType, activeParams.smoothingSpeed, speed);
    }

    private SharedRigClock GetAndUpdateSharedClock(float currentCycleSpeed)
    {
        if (rigRoot == null)
            rigRoot = transform.root != null ? transform.root : transform;

        if (!s_rigClocks.TryGetValue(rigRoot, out SharedRigClock clock))
        {
            clock = new SharedRigClock();
            s_rigClocks[rigRoot] = clock;
        }

        if (clock.lastUpdatedFrame != Time.frameCount)
        {
            // Reset clock if no leg was active on the previous frame so steps always start cleanly
            if (clock.lastUpdatedFrame != -1 && Time.frameCount - clock.lastUpdatedFrame > 1)
            {
                clock.elapsedActiveTime = 0f;
                clock.accumulatedCycles = 0f;
            }

            clock.elapsedActiveTime += Time.deltaTime;
            clock.accumulatedCycles += Time.deltaTime * currentCycleSpeed;
            clock.lastUpdatedFrame = Time.frameCount;
        }

        return clock;
    }

    private float CalculateNormalizedPhase(SharedRigClock clock, float cycleSpeed)
    {
        float singleStrideDuration = 1f / cycleSpeed;
        float totalPeriodSeconds = singleStrideDuration + Mathf.Max(0f, stepRestDelay);

        if (timingOffsetMode == TimingOffsetMode.NormalizedPhase)
        {
            // Fractional phase offset [0..1] (still works intuitively if > 1 by scaling across 4 legs if desired)
            float phaseOffset = timingOffset;
            if (stepRestDelay <= 0.0001f)
            {
                return Mathf.Repeat(clock.accumulatedCycles - phaseOffset, 1f);
            }

            float offsetTime = clock.elapsedActiveTime - (phaseOffset * totalPeriodSeconds);
            float timeInPeriod = Mathf.Repeat(offsetTime, totalPeriodSeconds);
            return Mathf.Clamp01(timeInPeriod / singleStrideDuration);
        }
        else
        {
            // Time-based offset in seconds: leg steps strictly timingOffset seconds after the base clock
            float delayedTime = clock.elapsedActiveTime - timingOffset;
            if (stepRestDelay <= 0.0001f)
            {
                return Mathf.Repeat(delayedTime * cycleSpeed, 1f);
            }

            float timeInPeriod = Mathf.Repeat(delayedTime, totalPeriodSeconds);
            return Mathf.Clamp01(timeInPeriod / singleStrideDuration);
        }
    }

    private void ApplyPositionSmoothing(Vector3 targetLocalPos, SmoothingType smoothing, float speed, float cycleSpeedMultiplier)
    {
        if (speed <= 0f)
        {
            ikTarget.localPosition = targetLocalPos;
            return;
        }

        // Scale smoothing rate by cycleSpeed so high-speed cycles don't lag behind their timingOffset
        float effectiveSpeed = speed * Mathf.Max(1f, cycleSpeedMultiplier);

        if (smoothing == SmoothingType.Linear)
        {
            ikTarget.localPosition = Vector3.MoveTowards(
                ikTarget.localPosition,
                targetLocalPos,
                effectiveSpeed * Time.deltaTime
            );
        }
        else
        {
            ikTarget.localPosition = Vector3.Lerp(
                ikTarget.localPosition,
                targetLocalPos,
                1f - Mathf.Exp(-effectiveSpeed * Time.deltaTime)
            );
        }
    }
}