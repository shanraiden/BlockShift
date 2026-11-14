using System.Collections.Generic;
using UnityEngine;

public class ProceduralFKLeg2D : MonoBehaviour
{
    public enum SmoothingType
    {
        Smooth,
        Linear,
        SnappySpring
    }

    public enum TimingOffsetMode
    {
        [Tooltip("Normalized cycle phase offset [0..1] (0.5 = 180 degrees opposite leg).")]
        NormalizedPhase,

        [Tooltip("Delays the leg by timingOffset (in seconds) and waits in rest pose before taking the first step.")]
        TimeDelayWithStartWait,

        [Tooltip("Continuous time offset in seconds without startup wait.")]
        TimeOffsetSeconds
    }

    public enum DParabolaMode
    {
        [Tooltip("Solves Thigh & Shin angles so the Follow Bone physically traces the 2D D-Parabola path in space.")]
        ExactPawTrajectoryIK,

        [Tooltip("Directly moves the Custom Follow Bone's position along the D-Parabola path while keeping FK leg rotations.")]
        DirectBonePositionFollow,

        [Tooltip("Drives Thigh, Shin, and Follow Bone FK angles using a parabolic lift curve (4*u*(1-u)) during swing and flat stance.")]
        ParabolicFKAngles
    }

    [System.Serializable]
    public struct GaitPreset
    {
        [Header("Cycle & Joint Amplitudes")]
        public float cycleSpeed;
        public float thighSwingAmplitude;
        public float shinFlexAmplitude;
        public float pawLagAmplitude;
        [Range(0.2f, 0.8f)]
        public float swingDutyRatio;
        public SmoothingType smoothingType;
        public float smoothingSpeed;

        [Header("Root Bone Position & Body Stretch / Height Drop")]
        [Tooltip("Optional override for the Root Bone / Sprite Transform to stretch and lower in this preset (uses the main Root Bone field if left empty).")]
        public Transform rootBoneOverride;

        [Tooltip("Local position offset applied to this leg's root bone (legBones[0]) during this state (e.g. shift hip forward/up when running).")]
        public Vector2 legRootPositionOffset;

        [Tooltip("Local position offset applied to the character's Root Bone / Body (e.g. negative Y decreases overall sprite height, X shifts forward).")]
        public Vector2 rootBonePositionOffset;

        [Tooltip("Scale multiplier for the Root Bone / Overall Sprite (e.g. X > 1 stretches body horizontally, Y < 1 decreases overall sprite height while running).")]
        public Vector2 bodyStretchScale;

        [Tooltip("Dynamic rhythmic stretch/squash pulse synced to the stride cycle while moving/running (0 = static stretch only).")]
        [Range(0f, 0.35f)]
        public float dynamicStretchPulse;

        [Tooltip("Forward lean/pitch angle (in degrees) applied to the Root Bone while in this state.")]
        public float rootBoneForwardLeanAngle;

        [Header("Running Crouch Posture & D-Parabola Multipliers")]
        [Tooltip("Additional Thigh angle offset (degrees) in this gait state.")]
        public float thighCrouchOffset;

        [Tooltip("Additional Shin/Knee flex offset (degrees) in this gait state to keep legs crouched while running.")]
        public float shinCrouchOffset;

        [Tooltip("Additional Paw/Foot angle offset (degrees) in this gait state.")]
        public float pawCrouchOffset;

        [Tooltip("Multiplier applied to D-Parabola stride length in this gait state.")]
        public float dParabolaStrideMultiplier;

        [Tooltip("Multiplier applied to D-Parabola step height in this gait state.")]
        public float dParabolaHeightMultiplier;
    }

    [System.Serializable]
    public struct StillMotionSettings
    {
        [Tooltip("Breathing/idle oscillation speed (cycles per second).")]
        public float breathingSpeed;

        [Tooltip("Subtle hip/thigh sway angle (degrees) while standing still.")]
        public float thighIdleSwayAngle;

        [Tooltip("Subtle knee flex/cushion angle (degrees) while standing still.")]
        public float shinIdleFlexAngle;

        [Tooltip("Subtle paw/ankle settle angle (degrees) while standing still.")]
        public float pawIdleAngle;

        [Tooltip("Phase delay (in radians) between Thigh, Shin, and Paw during still motion.")]
        public float jointPhaseLag;

        [Tooltip("Resting posture offset (degrees) for the Thigh when Still.")]
        public float thighStillPostureOffset;

        [Tooltip("Resting posture offset (degrees) for the Shin when Still.")]
        public float shinStillPostureOffset;

        [Tooltip("Resting posture offset (degrees) for the Paw when Still.")]
        public float pawStillPostureOffset;

        [Tooltip("Subtle vertical breathing bob on the hip bone while Still.")]
        public float hipBreathingBob;

        [Tooltip("How smoothly the leg blends into and follows Still motion.")]
        public float transitionSpeed;
    }

    [System.Serializable]
    public struct DParabolaSettings
    {
        [Tooltip("Assign any custom bone or Transform to follow the D-Parabola. If null, defaults to Paw (legBones[2] or last bone in legBones).")]
        public Transform customFollowBone;

        [Tooltip("How the D-Parabola is applied to the leg chain.")]
        public DParabolaMode mode;

        [Tooltip("If true, directly moves the Follow Bone's position along the D-Parabola curve.")]
        public bool lockFollowBoneToPathPosition;

        [Tooltip("Local (X, Y) offset to shift the center position of the D-Parabola relative to the Follow Bone's initial position.")]
        public Vector2 positionOffset;

        [Tooltip("Optional Transform to use as the center position of the D-Parabola. If null, uses the Follow Bone's initial position + positionOffset.")]
        public Transform customCenterTransform;

        [Tooltip("Rotates the entire D-Parabola path in degrees (0 = flat horizontal base).")]
        [Range(-90f, 90f)]
        public float pathRotationAngle;

        [Tooltip("Horizontal half-width of the flat bottom of the 'D' trajectory.")]
        public float strideLength;

        [Tooltip("Vertical peak height of the parabolic arch (y = 4 * H * u * (1 - u)).")]
        public float stepHeight;

        [Tooltip("Shifts the peak of the parabola backward (< 0.5) or forward (> 0.5). 0.5 = symmetric parabola.")]
        [Range(0.2f, 0.8f)]
        public float apexBias;

        [Tooltip("How much the follow bone tilts along the parabola's tangent slope during the swing arch (degrees).")]
        public float pawTangentTiltAngle;

        [Tooltip("How much the follow bone counter-rotates against Thigh+Shin during the flat bottom of the 'D' to stay flat on the ground [0..1].")]
        [Range(0f, 1f)]
        public float pawGroundLeveling;

        [Tooltip("Flips the knee bend direction when solving ExactPawTrajectoryIK.")]
        public bool flipIKKneeBend;

        [Tooltip("Draws the D-Parabola path and current target in the Scene View when selected.")]
        public bool showGizmo;
    }

    [Header("Bone Chain (0 = Thigh/Hip, 1 = Shin/Knee, 2 = Paw/Foot, 3+ = Toes/Tips)")]
    public List<Transform> legBones = new List<Transform>();

    [Tooltip("Optional character Root Bone / Body / Sprite Root Transform whose position lowers and scale stretches horizontally / decreases vertically while running.")]
    public Transform rootBone;

    [Header("State Booleans")]
    [Tooltip("Enable Still (idle/breathing) motion on the leg. When true, uses Still Motion Settings.")]
    public bool isStill = false;

    [Tooltip("Enable walking/moving leg cycle.")]
    public bool isMoving = true;

    [Tooltip("Enable running/sprinting leg cycle (takes priority over isMoving when both are checked).")]
    public bool isRunning = false;

    [Tooltip("If true, the Paw follows a D-Parabola trajectory (parabolic swing arch + flat linear ground sweep).")]
    public bool followDParabola = false;

    [Tooltip("If true, uses Moving/Running Gait Presets below based on isMoving/isRunning. If false, uses the direct Stride & Flexion settings when walking, and still applies Running Preset when isRunning is true.")]
    public bool useStatePresets = true;

    [Header("Still Motion Settings (When IsStill is True or Idle)")]
    public StillMotionSettings stillMotion = new StillMotionSettings
    {
        breathingSpeed = 1.8f,
        thighIdleSwayAngle = 2.0f,
        shinIdleFlexAngle = 3.5f,
        pawIdleAngle = 3.0f,
        jointPhaseLag = 0.8f,
        thighStillPostureOffset = 0f,
        shinStillPostureOffset = 0f,
        pawStillPostureOffset = 0f,
        hipBreathingBob = 0.015f,
        transitionSpeed = 10f
    };

    [Header("D-Parabola Paw Settings (When Follow D-Parabola is True)")]
    public DParabolaSettings dParabola = new DParabolaSettings
    {
        customFollowBone = null,
        mode = DParabolaMode.ExactPawTrajectoryIK,
        lockFollowBoneToPathPosition = false,
        positionOffset = Vector2.zero,
        customCenterTransform = null,
        pathRotationAngle = 0f,
        strideLength = 0.45f,
        stepHeight = 0.35f,
        apexBias = 0.5f,
        pawTangentTiltAngle = 18f,
        pawGroundLeveling = 0.75f,
        flipIKKneeBend = false,
        showGizmo = true
    };

    [Header("Cycle & Timing Offset Settings")]
    public TimingOffsetMode timingOffsetMode = TimingOffsetMode.NormalizedPhase;

    [Tooltip("Offset between legs (e.g. 0.0 and 0.5 in NormalizedPhase, or delay in seconds).")]
    [Range(0f, 4f)]
    public float timingOffset = 0f;

    [Tooltip("Strides per second (used when useStatePresets is false).")]
    public float cycleSpeed = 4.0f;

    [Tooltip("Optional pause duration (in seconds) at the end of each stride.")]
    [Range(0f, 2f)]
    public float stepRestDelay = 0f;

    [Header("Stride & Flexion Angles (Degrees)")]
    public float thighSwingAmplitude = 30f; // Forward/backward pendulum swing of the whole leg
    public float shinFlexAmplitude = 45f;   // How much the knee bends upward during the swing phase
    public float pawLagAmplitude = 15f;     // Subtle trailing flick at the paw joint

    [Header("Natural Gait Shaping & Joint Tuning")]
    [Tooltip("Fraction of the cycle spent swinging in the air vs pushing on the ground (0.5 = symmetric).")]
    [Range(0.2f, 0.8f)]
    public float swingDutyRatio = 0.45f;

    [Tooltip("Subtle knee bend during ground contact to absorb landing impact.")]
    [Range(0f, 0.5f)]
    public float stanceShockAbsorption = 0.15f;

    [Tooltip("Phase delay (in radians) of the knee relative to the thigh.")]
    public float shinPhaseLag = 1.2f;

    [Tooltip("Phase delay (in radians) of the paw relative to the thigh.")]
    public float pawPhaseLag = 1.65f;

    [Tooltip("Phase delay per bone for any additional toe/hock bones beyond index 2.")]
    public float extraBonePhaseLag = 0.55f;

    [Tooltip("Amplitude multiplier per extra bone beyond index 2.")]
    [Range(0.2f, 1.5f)]
    public float extraBoneAmplitudeFalloff = 0.75f;

    [Header("Direction & Posture Offsets")]
    public bool invertSwingDirection = false;
    public bool invertKneeBend = false;
    public bool invertPawBend = false;
    public float thighPostureOffset = 0f;
    public float shinPostureOffset = 0f;
    public float pawPostureOffset = 0f;

    [Header("Hip Vertical Bounce (Optional)")]
    public bool enableHipBob = false;
    public float hipBobAmplitude = 0.05f;
    public float hipBobPhaseOffset = 0f;

    [Header("Smoothing & Idle Return")]
    public SmoothingType smoothingType = SmoothingType.Smooth;
    public float smoothingSpeed = 18f;
    public float idleReturnSpeed = 10f;

    [Header("State Presets (Used When 'Use State Presets' Is Checked)")]
    public GaitPreset movingPreset = new GaitPreset
    {
        cycleSpeed = 2.5f,
        thighSwingAmplitude = 24f,
        shinFlexAmplitude = 35f,
        pawLagAmplitude = 12f,
        swingDutyRatio = 0.45f,
        smoothingType = SmoothingType.Smooth,
        smoothingSpeed = 16f,
        rootBoneOverride = null,
        legRootPositionOffset = Vector2.zero,
        rootBonePositionOffset = Vector2.zero,
        bodyStretchScale = Vector2.one,
        dynamicStretchPulse = 0.02f,
        rootBoneForwardLeanAngle = 0f,
        thighCrouchOffset = 0f,
        shinCrouchOffset = 0f,
        pawCrouchOffset = 0f,
        dParabolaStrideMultiplier = 1f,
        dParabolaHeightMultiplier = 1f
    };

    public GaitPreset runningPreset = new GaitPreset
    {
        cycleSpeed = 4.5f,
        thighSwingAmplitude = 38f,
        shinFlexAmplitude = 55f,
        pawLagAmplitude = 22f,
        swingDutyRatio = 0.4f,
        smoothingType = SmoothingType.SnappySpring,
        smoothingSpeed = 24f,
        rootBoneOverride = null,
        legRootPositionOffset = new Vector2(0f, -0.05f),
        rootBonePositionOffset = new Vector2(0.05f, -0.14f),
        bodyStretchScale = new Vector2(1.16f, 0.85f),
        dynamicStretchPulse = 0.05f,
        rootBoneForwardLeanAngle = -6f,
        thighCrouchOffset = 8f,
        shinCrouchOffset = 10f,
        pawCrouchOffset = 0f,
        dParabolaStrideMultiplier = 1.35f,
        dParabolaHeightMultiplier = 1.2f
    };

    private class SharedFKClock
    {
        public int lastUpdatedFrame = -1;
        public float elapsedActiveTime = 0f;
        public float accumulatedCycles = 0f;
    }

    private class SharedRootBoneState
    {
        public Vector3 initialLocalPos;
        public Vector3 initialAbsLocalScale;
        public Quaternion initialLocalRot;
        public Quaternion currentLocalRot;
        public int lastUpdatedFrame = -1;
    }

    private static readonly Dictionary<Transform, SharedFKClock> s_rigClocks = new Dictionary<Transform, SharedFKClock>();
    private static readonly Dictionary<Transform, SharedRootBoneState> s_rootBoneStates = new Dictionary<Transform, SharedRootBoneState>();

    private Quaternion[] initialLocalRotations;
    private Quaternion[] currentLocalRotations;
    private Vector3 initialHipLocalPos;
    private Transform rigRoot;
    private bool wasActiveLastFrame;
    private float legActiveElapsed;
    private float stillTimer;

    // Cached 2-bone geometry for ExactPawTrajectoryIK D-Parabola solving
    private float thighLength;
    private float shinLength;
    private Vector2 initialPawOffsetInRoot;
    private float restThighAngleRad;
    private float restShinRelAngleRad;
    private float defaultKneeBendSign = 1f;
    private Vector3 lastParabolaTargetWorld;

    // Custom Follow Bone cached state
    private Transform cachedFollowBone;
    private Vector3 initialFollowBoneLocalPos;
    private Quaternion initialFollowBoneLocalRot;
    private Quaternion currentFollowBoneLocalRot;

    public bool IsStill
    {
        get => isStill;
        set => isStill = value;
    }

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

    public bool FollowDParabola
    {
        get => followDParabola;
        set => followDParabola = value;
    }

    /// <summary>
    /// Returns the custom follow bone if assigned, otherwise falls back to legBones[2] (Paw) or the last bone in legBones.
    /// </summary>
    public Transform GetActiveFollowBone()
    {
        if (dParabola.customFollowBone != null)
            return dParabola.customFollowBone;

        if (legBones != null && legBones.Count > 0)
        {
            int fallbackIndex = Mathf.Min(2, legBones.Count - 1);
            return legBones[fallbackIndex];
        }

        return null;
    }

    /// <summary>
    /// Returns the active Root Bone to stretch/lower for the given preset (preset override or component rootBone).
    /// </summary>
    public Transform GetActiveRootBone(in GaitPreset preset)
    {
        if (preset.rootBoneOverride != null)
            return preset.rootBoneOverride;

        return rootBone;
    }

    private void OnValidate()
    {
        EnsurePresetDefaults();
    }

    private void EnsurePresetDefaults()
    {
        if (movingPreset.bodyStretchScale.sqrMagnitude < 0.0001f)
            movingPreset.bodyStretchScale = Vector2.one;
        if (movingPreset.dParabolaStrideMultiplier <= 0.0001f)
            movingPreset.dParabolaStrideMultiplier = 1f;
        if (movingPreset.dParabolaHeightMultiplier <= 0.0001f)
            movingPreset.dParabolaHeightMultiplier = 1f;

        if (runningPreset.bodyStretchScale.sqrMagnitude < 0.0001f)
            runningPreset.bodyStretchScale = new Vector2(1.16f, 0.85f);
        if (runningPreset.dParabolaStrideMultiplier <= 0.0001f)
            runningPreset.dParabolaStrideMultiplier = 1.35f;
        if (runningPreset.dParabolaHeightMultiplier <= 0.0001f)
            runningPreset.dParabolaHeightMultiplier = 1.2f;
    }

    void Start()
    {
        EnsurePresetDefaults();
        CacheInitialPose();
    }

    [ContextMenu("Recapture Initial Pose")]
    public void CacheInitialPose()
    {
        rigRoot = transform.root != null ? transform.root : transform;

        if (legBones != null && legBones.Count > 0)
        {
            initialLocalRotations = new Quaternion[legBones.Count];
            currentLocalRotations = new Quaternion[legBones.Count];

            for (int i = 0; i < legBones.Count; i++)
            {
                if (legBones[i] != null)
                {
                    initialLocalRotations[i] = legBones[i].localRotation;
                    currentLocalRotations[i] = legBones[i].localRotation;
                }
            }

            if (legBones[0] != null)
            {
                initialHipLocalPos = legBones[0].localPosition;
            }
        }

        RegisterRootBoneState(rootBone, overwriteExisting: true);
        RegisterRootBoneState(movingPreset.rootBoneOverride, overwriteExisting: true);
        RegisterRootBoneState(runningPreset.rootBoneOverride, overwriteExisting: true);

        CacheFollowBoneGeometry();
    }

    private SharedRootBoneState RegisterRootBoneState(Transform targetRoot, bool overwriteExisting = false)
    {
        if (targetRoot == null) return null;

        if (!s_rootBoneStates.TryGetValue(targetRoot, out SharedRootBoneState state))
        {
            state = new SharedRootBoneState();
            s_rootBoneStates[targetRoot] = state;
            overwriteExisting = true;
        }

        if (overwriteExisting)
        {
            state.initialLocalPos = targetRoot.localPosition;
            Vector3 s = targetRoot.localScale;
            state.initialAbsLocalScale = new Vector3(
                Mathf.Max(0.0001f, Mathf.Abs(s.x)),
                Mathf.Max(0.0001f, Mathf.Abs(s.y)),
                Mathf.Max(0.0001f, Mathf.Abs(s.z))
            );
            state.initialLocalRot = targetRoot.localRotation;
            state.currentLocalRot = targetRoot.localRotation;
        }

        return state;
    }

    private void CacheFollowBoneGeometry()
    {
        if (rigRoot == null)
            rigRoot = transform.root != null ? transform.root : transform;

        cachedFollowBone = GetActiveFollowBone();
        if (cachedFollowBone != null)
        {
            initialFollowBoneLocalPos = cachedFollowBone.localPosition;
            initialFollowBoneLocalRot = cachedFollowBone.localRotation;
            currentFollowBoneLocalRot = cachedFollowBone.localRotation;
        }

        // Cache 2-bone rest geometry in rigRoot space for D-Parabola IK using legBones[0], legBones[1], and active Follow Bone
        Transform hipBone = (legBones != null && legBones.Count > 0 && legBones[0] != null) ? legBones[0] : transform;
        Transform kneeBone = (legBones != null && legBones.Count > 1 && legBones[1] != null) ? legBones[1] : null;

        if (hipBone != null && cachedFollowBone != null)
        {
            Vector2 p0 = rigRoot.InverseTransformPoint(hipBone.position);
            Vector2 p2 = rigRoot.InverseTransformPoint(cachedFollowBone.position);
            initialPawOffsetInRoot = p2 - p0;

            if (kneeBone != null && kneeBone != cachedFollowBone)
            {
                Vector2 p1 = rigRoot.InverseTransformPoint(kneeBone.position);
                Vector2 v01 = p1 - p0;
                Vector2 v12 = p2 - p1;

                thighLength = Mathf.Max(0.001f, v01.magnitude);
                shinLength = Mathf.Max(0.001f, v12.magnitude);

                restThighAngleRad = Mathf.Atan2(v01.y, v01.x);
                float absShinAngleRad = Mathf.Atan2(v12.y, v12.x);
                restShinRelAngleRad = Mathf.DeltaAngle(restThighAngleRad * Mathf.Rad2Deg, absShinAngleRad * Mathf.Rad2Deg) * Mathf.Deg2Rad;
                defaultKneeBendSign = Mathf.Abs(restShinRelAngleRad) > 0.001f ? Mathf.Sign(restShinRelAngleRad) : 1f;
            }
        }
    }

    void LateUpdate()
    {
        bool hasLegBones = legBones != null && legBones.Count > 0;
        if (!hasLegBones && dParabola.customFollowBone == null) return;

        if (hasLegBones && (initialLocalRotations == null || initialLocalRotations.Length != legBones.Count))
        {
            CacheInitialPose();
        }
        else if (cachedFollowBone != GetActiveFollowBone())
        {
            CacheFollowBoneGeometry();
        }

        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        // If isStill is checked, or neither isMoving nor isRunning is active, run Still Motion
        bool shouldRunStillMotion = isStill || (!isMoving && !isRunning);

        if (shouldRunStillMotion)
        {
            wasActiveLastFrame = false;
            legActiveElapsed = 0f;
            ApplyStillMotion(dt);
            return;
        }

        // Resolve active gait preset & parameters (Running always activates runningPreset features)
        GaitPreset activePreset = isRunning ? runningPreset : movingPreset;
        float activeCycleSpeed = cycleSpeed;
        float activeThighAmp = thighSwingAmplitude;
        float activeShinAmp = shinFlexAmplitude;
        float activePawAmp = pawLagAmplitude;
        float activeDuty = swingDutyRatio;
        SmoothingType activeSmoothing = smoothingType;
        float activeSmoothSpeed = smoothingSpeed;

        if (useStatePresets || isRunning)
        {
            activeCycleSpeed = activePreset.cycleSpeed;
            activeThighAmp = activePreset.thighSwingAmplitude;
            activeShinAmp = activePreset.shinFlexAmplitude;
            activePawAmp = activePreset.pawLagAmplitude;
            activeDuty = activePreset.swingDutyRatio;
            activeSmoothing = activePreset.smoothingType;
            activeSmoothSpeed = activePreset.smoothingSpeed;
        }

        activeCycleSpeed = Mathf.Max(0.001f, activeCycleSpeed);

        if (!wasActiveLastFrame)
        {
            wasActiveLastFrame = true;
            legActiveElapsed = 0f;
        }

        legActiveElapsed += dt;

        SharedFKClock clock = GetAndUpdateSharedClock(activeCycleSpeed, dt);

        if (timingOffsetMode == TimingOffsetMode.TimeDelayWithStartWait && legActiveElapsed < timingOffset)
        {
            ApplyStillMotion(dt);
            return;
        }

        // Normalized stride phase [0..1]
        float normalizedPhase = CalculateNormalizedPhase(clock, activeCycleSpeed);
        float warpedPhase = RemapPhaseWithDutyRatio(normalizedPhase, activeDuty);

        float swingDir = invertSwingDirection ? -1f : 1f;
        float kneeDir = invertKneeBend ? -1f : 1f;
        float pawDir = invertPawBend ? -1f : 1f;

        float totalThighPosture = thighPostureOffset + (activePreset.thighCrouchOffset * swingDir);
        float totalShinPosture = shinPostureOffset + (activePreset.shinCrouchOffset * kneeDir);
        float totalPawPosture = pawPostureOffset + (activePreset.pawCrouchOffset * pawDir);

        float strideMult = activePreset.dParabolaStrideMultiplier > 0.0001f ? activePreset.dParabolaStrideMultiplier : 1f;
        float heightMult = activePreset.dParabolaHeightMultiplier > 0.0001f ? activePreset.dParabolaHeightMultiplier : 1f;

        float thighAngle;
        float shinAngle;
        float pawAngle;

        if (followDParabola)
        {
            EvaluateDParabolaGait(
                normalizedPhase,
                activeDuty,
                activeThighAmp,
                activeShinAmp,
                activePawAmp,
                swingDir,
                kneeDir,
                pawDir,
                totalThighPosture,
                totalShinPosture,
                totalPawPosture,
                strideMult,
                heightMult,
                out thighAngle,
                out shinAngle,
                out pawAngle
            );
        }
        else
        {
            // Standard FK Wave Gait
            // 1. Thigh: Asymmetric pendulum swing with optional linear vs smooth shaping
            float thighWave = EvaluateWave(warpedPhase, activeSmoothing);
            thighAngle = (thighWave * activeThighAmp * swingDir) + totalThighPosture;

            // 2. Shin (Knee): Lifts sharply during swing phase + subtle shock absorption during stance
            float shinPhase = warpedPhase - shinPhaseLag;
            float primaryKneeLift = Mathf.Max(0f, Mathf.Sin(shinPhase));
            float stanceCompression = Mathf.Max(0f, -Mathf.Sin(shinPhase)) * stanceShockAbsorption;
            float shinNormalizedBend = Mathf.Clamp01(primaryKneeLift + stanceCompression);
            shinAngle = (shinNormalizedBend * activeShinAmp * kneeDir) + totalShinPosture;

            // 3. Paw (Ankle/Foot): Trailing whip lag + toe-off flick
            float pawPhase = warpedPhase - pawPhaseLag;
            float pawWave = Mathf.Cos(pawPhase) + 0.25f * Mathf.Sin(pawPhase * 2f);
            pawAngle = (pawWave * activePawAmp * pawDir) + totalPawPosture;
        }

        float pawFollowPhase = warpedPhase - pawPhaseLag;
        Transform activeFollowBone = GetActiveFollowBone();
        bool followBoneInChain = false;

        // Apply rotations to Thigh (0), Shin (1), Paw (2), and any extra chain bones (3+)
        if (hasLegBones)
        {
            for (int i = 0; i < legBones.Count; i++)
            {
                if (legBones[i] == null) continue;

                if (legBones[i] == activeFollowBone)
                    followBoneInChain = true;

                float targetAngle;
                if (followDParabola && legBones[i] == activeFollowBone)
                {
                    targetAngle = pawAngle;
                }
                else if (i == 0)
                {
                    targetAngle = thighAngle;
                }
                else if (i == 1)
                {
                    targetAngle = shinAngle;
                }
                else if (i == 2)
                {
                    targetAngle = pawAngle;
                }
                else
                {
                    int extraIndex = i - 2;
                    float falloff = Mathf.Pow(extraBoneAmplitudeFalloff, extraIndex);
                    float extraPhase = pawFollowPhase - (extraIndex * extraBonePhaseLag);
                    targetAngle = Mathf.Cos(extraPhase) * activePawAmp * falloff * pawDir;
                }

                Quaternion targetRot = initialLocalRotations[i] * Quaternion.Euler(0f, 0f, targetAngle);
                ApplyBoneRotation(i, targetRot, activeSmoothing, activeSmoothSpeed, activeCycleSpeed, dt);
            }
        }

        // If customFollowBone is outside legBones, apply D-Parabola rotation to it as well
        if (followDParabola && activeFollowBone != null && !followBoneInChain)
        {
            Quaternion targetRot = initialFollowBoneLocalRot * Quaternion.Euler(0f, 0f, pawAngle);
            float effectiveSpeed = activeSmoothSpeed * Mathf.Max(1f, activeCycleSpeed * 0.5f);
            float t = activeSmoothSpeed <= 0f ? 1f : (1f - Mathf.Exp(-effectiveSpeed * dt));
            currentFollowBoneLocalRot = Quaternion.Slerp(currentFollowBoneLocalRot, targetRot, t);
            activeFollowBone.localRotation = currentFollowBoneLocalRot;
        }

        // If DirectBonePositionFollow or lockFollowBoneToPathPosition is enabled, move the Follow Bone directly onto the D-Parabola point
        if (followDParabola && activeFollowBone != null &&
            (dParabola.mode == DParabolaMode.DirectBonePositionFollow || dParabola.lockFollowBoneToPathPosition))
        {
            float effectiveSpeed = activeSmoothSpeed * Mathf.Max(1f, activeCycleSpeed);
            if (activeSmoothSpeed <= 0f)
            {
                activeFollowBone.position = lastParabolaTargetWorld;
            }
            else if (activeSmoothing == SmoothingType.Linear)
            {
                activeFollowBone.position = Vector3.MoveTowards(activeFollowBone.position, lastParabolaTargetWorld, effectiveSpeed * dt);
            }
            else
            {
                activeFollowBone.position = Vector3.Lerp(activeFollowBone.position, lastParabolaTargetWorld, 1f - Mathf.Exp(-effectiveSpeed * dt));
            }
        }

        // Apply Leg Root Bone (legBones[0]) position offset + optional vertical hip bounce
        if (hasLegBones && legBones[0] != null)
        {
            float bobY = enableHipBob ? (Mathf.Sin(warpedPhase * 2f + hipBobPhaseOffset) * hipBobAmplitude) : 0f;
            Vector3 legRootOffset = new Vector3(
                activePreset.legRootPositionOffset.x * swingDir,
                activePreset.legRootPositionOffset.y + bobY,
                0f
            );
            Vector3 targetHipPos = initialHipLocalPos + legRootOffset;
            float posLerpT = activeSmoothSpeed <= 0f ? 1f : (1f - Mathf.Exp(-activeSmoothSpeed * dt));
            legBones[0].localPosition = Vector3.Lerp(legBones[0].localPosition, targetHipPos, posLerpT);
        }

        // Apply Character Root Bone position offset, horizontal body stretch, overall height decrease, and forward lean
        ApplyRootBonePresetTransform(in activePreset, warpedPhase, swingDir, activeSmoothSpeed, dt);
    }

    private void ApplyRootBonePresetTransform(in GaitPreset preset, float warpedPhase, float swingDir, float smoothSpeed, float dt)
    {
        Transform targetRoot = GetActiveRootBone(in preset);
        if (targetRoot == null) return;

        SharedRootBoneState state = RegisterRootBoneState(targetRoot, overwriteExisting: false);
        if (state == null || state.lastUpdatedFrame == Time.frameCount)
            return;

        state.lastUpdatedFrame = Time.frameCount;
        float lerpT = smoothSpeed <= 0f ? 1f : (1f - Mathf.Exp(-smoothSpeed * dt));

        // 1. Root Bone local position offset (e.g. negative Y lowers the height of the overall sprite while running)
        Vector3 targetLocalPos = state.initialLocalPos + new Vector3(
            preset.rootBonePositionOffset.x * swingDir,
            preset.rootBonePositionOffset.y,
            0f
        );
        targetRoot.localPosition = Vector3.Lerp(targetRoot.localPosition, targetLocalPos, lerpT);

        // 2. Body Stretch & Height Decrease (X > 1 stretches body horizontally, Y < 1 decreases overall sprite height)
        float baseStretchX = preset.bodyStretchScale.x > 0.001f ? preset.bodyStretchScale.x : 1f;
        float baseStretchY = preset.bodyStretchScale.y > 0.001f ? preset.bodyStretchScale.y : 1f;

        // Rhythmic running stretch/squash pulse (2 pulses per full stride cycle)
        float pulseWave = Mathf.Sin(warpedPhase * 2f) * preset.dynamicStretchPulse;
        float finalStretchX = Mathf.Max(0.1f, baseStretchX + pulseWave);
        float finalStretchY = Mathf.Max(0.1f, baseStretchY - pulseWave * 0.5f);

        Vector3 currentScale = targetRoot.localScale;
        float signX = currentScale.x < 0f ? -1f : 1f;
        float signY = currentScale.y < 0f ? -1f : 1f;
        float signZ = currentScale.z < 0f ? -1f : 1f;

        Vector3 targetScale = new Vector3(
            signX * state.initialAbsLocalScale.x * finalStretchX,
            signY * state.initialAbsLocalScale.y * finalStretchY,
            signZ * state.initialAbsLocalScale.z
        );

        // Preserve instantaneous sign flips on X (from PlayerMovement directional flipping) while smoothly interpolating magnitude
        float nextAbsX = Mathf.Lerp(Mathf.Abs(currentScale.x), Mathf.Abs(targetScale.x), lerpT);
        float nextAbsY = Mathf.Lerp(Mathf.Abs(currentScale.y), Mathf.Abs(targetScale.y), lerpT);
        float nextAbsZ = Mathf.Lerp(Mathf.Abs(currentScale.z), Mathf.Abs(targetScale.z), lerpT);
        targetRoot.localScale = new Vector3(signX * nextAbsX, signY * nextAbsY, signZ * nextAbsZ);

        // 3. Forward Lean Rotation on Root Bone
        Quaternion targetRot = state.initialLocalRot * Quaternion.Euler(0f, 0f, preset.rootBoneForwardLeanAngle * swingDir);
        state.currentLocalRot = Quaternion.Slerp(state.currentLocalRot, targetRot, lerpT);
        targetRoot.localRotation = state.currentLocalRot;
    }

    private void ResetRootBoneToStill(Transform targetRoot, float blendRate)
    {
        if (targetRoot == null) return;

        SharedRootBoneState state = RegisterRootBoneState(targetRoot, overwriteExisting: false);
        if (state == null || state.lastUpdatedFrame == Time.frameCount)
            return;

        state.lastUpdatedFrame = Time.frameCount;

        targetRoot.localPosition = Vector3.Lerp(targetRoot.localPosition, state.initialLocalPos, blendRate);

        Vector3 currentScale = targetRoot.localScale;
        float signX = currentScale.x < 0f ? -1f : 1f;
        float signY = currentScale.y < 0f ? -1f : 1f;
        float signZ = currentScale.z < 0f ? -1f : 1f;

        float nextAbsX = Mathf.Lerp(Mathf.Abs(currentScale.x), state.initialAbsLocalScale.x, blendRate);
        float nextAbsY = Mathf.Lerp(Mathf.Abs(currentScale.y), state.initialAbsLocalScale.y, blendRate);
        float nextAbsZ = Mathf.Lerp(Mathf.Abs(currentScale.z), state.initialAbsLocalScale.z, blendRate);
        targetRoot.localScale = new Vector3(signX * nextAbsX, signY * nextAbsY, signZ * nextAbsZ);

        state.currentLocalRot = Quaternion.Slerp(state.currentLocalRot, state.initialLocalRot, blendRate);
        targetRoot.localRotation = state.currentLocalRot;
    }

    /// <summary>
    /// Applies subtle breathing/idle Still Motion when isStill is true (or when neither moving nor running).
    /// </summary>
    private void ApplyStillMotion(float dt)
    {
        stillTimer += dt * stillMotion.breathingSpeed * Mathf.PI * 2f;
        float phaseOffsetRad = timingOffset * Mathf.PI * 2f;
        float basePhase = stillTimer - phaseOffsetRad;

        float swingDir = invertSwingDirection ? -1f : 1f;
        float kneeDir = invertKneeBend ? -1f : 1f;
        float pawDir = invertPawBend ? -1f : 1f;

        float thighAngle = (Mathf.Sin(basePhase) * stillMotion.thighIdleSwayAngle * swingDir) + stillMotion.thighStillPostureOffset;
        float shinAngle = ((Mathf.Sin(basePhase - stillMotion.jointPhaseLag) * 0.5f + 0.5f) * stillMotion.shinIdleFlexAngle * kneeDir) + stillMotion.shinStillPostureOffset;
        float pawAngle = (Mathf.Cos(basePhase - stillMotion.jointPhaseLag * 2f) * stillMotion.pawIdleAngle * pawDir) + stillMotion.pawStillPostureOffset;

        float blendRate = stillMotion.transitionSpeed <= 0f ? 1f : (1f - Mathf.Exp(-stillMotion.transitionSpeed * dt));
        Transform activeFollowBone = GetActiveFollowBone();
        bool followBoneInChain = false;

        if (legBones != null)
        {
            for (int i = 0; i < legBones.Count; i++)
            {
                if (legBones[i] == null) continue;
                if (legBones[i] == activeFollowBone) followBoneInChain = true;

                float angle;
                if (i == 0) angle = thighAngle;
                else if (i == 1) angle = shinAngle;
                else if (i == 2) angle = pawAngle;
                else
                {
                    int extraIndex = i - 2;
                    float falloff = Mathf.Pow(extraBoneAmplitudeFalloff, extraIndex);
                    angle = Mathf.Cos(basePhase - (extraIndex + 2) * stillMotion.jointPhaseLag) * stillMotion.pawIdleAngle * falloff * pawDir;
                }

                Quaternion targetRot = initialLocalRotations[i] * Quaternion.Euler(0f, 0f, angle);
                currentLocalRotations[i] = Quaternion.Slerp(currentLocalRotations[i], targetRot, blendRate);
                legBones[i].localRotation = currentLocalRotations[i];
            }

            if (legBones.Count > 0 && legBones[0] != null)
            {
                float bobY = Mathf.Sin(basePhase) * stillMotion.hipBreathingBob;
                Vector3 targetHipPos = initialHipLocalPos + new Vector3(0f, bobY, 0f);
                legBones[0].localPosition = Vector3.Lerp(legBones[0].localPosition, targetHipPos, blendRate);
            }
        }

        if (activeFollowBone != null)
        {
            if (!followBoneInChain)
            {
                Quaternion targetRot = initialFollowBoneLocalRot * Quaternion.Euler(0f, 0f, pawAngle);
                currentFollowBoneLocalRot = Quaternion.Slerp(currentFollowBoneLocalRot, targetRot, blendRate);
                activeFollowBone.localRotation = currentFollowBoneLocalRot;
            }

            if (dParabola.mode == DParabolaMode.DirectBonePositionFollow || dParabola.lockFollowBoneToPathPosition)
            {
                activeFollowBone.localPosition = Vector3.Lerp(activeFollowBone.localPosition, initialFollowBoneLocalPos, blendRate);
            }
        }

        // Smoothly restore Root Bone position, stretch scale, and lean rotation when returning to Still Motion
        ResetRootBoneToStill(rootBone, blendRate);
        ResetRootBoneToStill(movingPreset.rootBoneOverride, blendRate);
        ResetRootBoneToStill(runningPreset.rootBoneOverride, blendRate);
    }

    /// <summary>
    /// Evaluates the D-Parabola trajectory (parabolic arch during swing + flat linear sweep during stance)
    /// and computes Thigh, Shin, and Follow Bone angles/positions so the Follow Bone traces the D-Parabola.
    /// </summary>
    private void EvaluateDParabolaGait(
        float normalizedPhase,
        float dutyRatio,
        float activeThighAmp,
        float activeShinAmp,
        float activePawAmp,
        float swingDir,
        float kneeDir,
        float pawDir,
        float totalThighPosture,
        float totalShinPosture,
        float totalPawPosture,
        float strideMultiplier,
        float heightMultiplier,
        out float thighAngle,
        out float shinAngle,
        out float pawAngle)
    {
        dutyRatio = Mathf.Clamp(dutyRatio, 0.15f, 0.85f);
        bool isSwing = normalizedPhase < dutyRatio;

        float xNorm;          // [-1 .. +1] horizontal progression along the 'D'
        float parabolaLift;   // [0 .. 1] parabolic vertical arch (4 * u * (1 - u))
        float parabolaSlope;  // derivative [-1 .. +1] of the parabola for paw tangent tilt

        if (isSwing)
        {
            // Swing phase [0 .. 1]: Follow Bone lifts along the top parabolic arch of the 'D'
            float u = normalizedPhase / dutyRatio;
            xNorm = Mathf.Lerp(-1f, 1f, u);

            // Remap u so the parabola peak occurs at dParabola.apexBias (default 0.5)
            float bias = Mathf.Clamp(dParabola.apexBias, 0.2f, 0.8f);
            float biasedU = u < bias
                ? (u / bias) * 0.5f
                : 0.5f + ((u - bias) / (1f - bias)) * 0.5f;

            // Classic parabola equation: y = 4 * u * (1 - u), peaking at 1.0 when biasedU = 0.5
            parabolaLift = 4f * biasedU * (1f - biasedU);
            // Normalized derivative dy/du in [-1 .. +1]: +1 at liftoff, 0 at apex, -1 at landing
            parabolaSlope = 1f - 2f * biasedU;
        }
        else
        {
            // Stance phase [0 .. 1]: Follow Bone moves linearly along the flat straight base of the 'D'
            float v = (normalizedPhase - dutyRatio) / (1f - dutyRatio);
            xNorm = Mathf.Lerp(1f, -1f, v);
            parabolaLift = 0f;
            parabolaSlope = 0f;
        }

        float effectiveStrideLength = dParabola.strideLength * strideMultiplier;
        float effectiveStepHeight = dParabola.stepHeight * heightMultiplier;

        Vector2 localParabolaPoint = new Vector2(
            xNorm * effectiveStrideLength * swingDir,
            parabolaLift * effectiveStepHeight
        );

        // Rotate the D-Parabola point if pathRotationAngle is non-zero
        if (Mathf.Abs(dParabola.pathRotationAngle) > 0.001f)
        {
            localParabolaPoint = RotateVector2(localParabolaPoint, dParabola.pathRotationAngle * Mathf.Deg2Rad);
        }

        // Compute world-space D-Parabola target point for IK, DirectBonePositionFollow, and Gizmos
        Transform hipBone = (legBones != null && legBones.Count > 0 && legBones[0] != null) ? legBones[0] : transform;
        Vector2 centerOffsetFromHip = initialPawOffsetInRoot + dParabola.positionOffset;

        if (rigRoot != null && hipBone != null)
        {
            Vector2 hipRootPos = rigRoot.InverseTransformPoint(hipBone.position);
            if (dParabola.customCenterTransform != null)
            {
                Vector2 customCenterRootPos = rigRoot.InverseTransformPoint(dParabola.customCenterTransform.position);
                centerOffsetFromHip = (customCenterRootPos - hipRootPos) + dParabola.positionOffset;
            }

            Vector2 targetFromHip = centerOffsetFromHip + localParabolaPoint;
            lastParabolaTargetWorld = rigRoot.TransformPoint(hipRootPos + targetFromHip);

            // Solve Thigh & Shin via 2-bone analytical IK if ExactPawTrajectoryIK is selected and we have valid 2-bone lengths
            if (dParabola.mode == DParabolaMode.ExactPawTrajectoryIK && legBones != null && legBones.Count >= 2 && thighLength > 0.001f && shinLength > 0.001f)
            {
                float dist = targetFromHip.magnitude;
                float minReach = Mathf.Abs(thighLength - shinLength) + 0.0005f;
                float maxReach = (thighLength + shinLength) - 0.0005f;
                float clampedDist = Mathf.Clamp(dist, minReach, maxReach);

                float baseAngleRad = Mathf.Atan2(targetFromHip.y, targetFromHip.x);

                float cosBeta = Mathf.Clamp(
                    (thighLength * thighLength + clampedDist * clampedDist - shinLength * shinLength) / (2f * thighLength * clampedDist),
                    -1f, 1f
                );
                float cosGamma = Mathf.Clamp(
                    (thighLength * thighLength + shinLength * shinLength - clampedDist * clampedDist) / (2f * thighLength * shinLength),
                    -1f, 1f
                );

                float betaRad = Mathf.Acos(cosBeta);
                float gammaRad = Mathf.Acos(cosGamma);

                float bendSign = defaultKneeBendSign * (dParabola.flipIKKneeBend ? -1f : 1f) * (invertKneeBend ? -1f : 1f);

                float solvedThighRad = baseAngleRad - bendSign * betaRad;
                float solvedShinRelRad = bendSign * (Mathf.PI - gammaRad);

                // Convert to delta degrees relative to the captured rest pose
                thighAngle = Mathf.DeltaAngle(restThighAngleRad * Mathf.Rad2Deg, solvedThighRad * Mathf.Rad2Deg) + totalThighPosture;
                shinAngle = Mathf.DeltaAngle(restShinRelAngleRad * Mathf.Rad2Deg, solvedShinRelRad * Mathf.Rad2Deg) + totalShinPosture;

                // Follow Bone follows the parabola tangent slope during swing and levels flat on the ground during stance
                float groundCompensation = -(thighAngle + shinAngle) * dParabola.pawGroundLeveling + dParabola.pathRotationAngle;
                float tangentTilt = isSwing ? (parabolaSlope * dParabola.pawTangentTiltAngle * pawDir) : 0f;
                pawAngle = groundCompensation + tangentTilt + totalPawPosture;
                return;
            }
        }

        // Parabolic FK Angle synthesis (used for ParabolicFKAngles and DirectBonePositionFollow):
        float horizBiasAngle = dParabola.positionOffset.x * 15f * swingDir;
        float vertBiasAngle = dParabola.positionOffset.y * 15f * kneeDir;

        thighAngle = (xNorm * activeThighAmp * swingDir) + horizBiasAngle + totalThighPosture;

        float stanceCompress = !isSwing
            ? Mathf.Sin(((normalizedPhase - dutyRatio) / (1f - dutyRatio)) * Mathf.PI) * stanceShockAbsorption
            : 0f;
        shinAngle = (Mathf.Clamp01(parabolaLift + stanceCompress) * activeShinAmp * kneeDir) + vertBiasAngle + totalShinPosture;

        float groundLevel = !isSwing ? (-thighAngle * dParabola.pawGroundLeveling * 0.5f) : 0f;
        float swingTilt = isSwing ? (parabolaSlope * (dParabola.pawTangentTiltAngle + activePawAmp * 0.5f) * pawDir) : 0f;
        pawAngle = groundLevel + swingTilt + dParabola.pathRotationAngle + totalPawPosture;
    }

    private static Vector2 RotateVector2(Vector2 v, float radians)
    {
        float cos = Mathf.Cos(radians);
        float sin = Mathf.Sin(radians);
        return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
    }

    private float RemapPhaseWithDutyRatio(float normalizedPhase, float dutyRatio)
    {
        dutyRatio = Mathf.Clamp(dutyRatio, 0.15f, 0.85f);

        // Maps [0..dutyRatio) to [0..PI) (Swing) and [dutyRatio..1) to [PI..2*PI) (Stance)
        if (normalizedPhase < dutyRatio)
        {
            float swingT = normalizedPhase / dutyRatio;
            return swingT * Mathf.PI;
        }
        else
        {
            float stanceT = (normalizedPhase - dutyRatio) / (1f - dutyRatio);
            return Mathf.PI + stanceT * Mathf.PI;
        }
    }

    private float EvaluateWave(float phaseRadians, SmoothingType mode)
    {
        float rawSine = Mathf.Sin(phaseRadians);

        switch (mode)
        {
            case SmoothingType.Linear:
                // Triangle wave in [-1..1] for constant angular velocity sweep
                float t = Mathf.Repeat(phaseRadians / (Mathf.PI * 2f), 1f);
                if (t < 0.25f) return t * 4f;
                if (t < 0.75f) return 2f - t * 4f;
                return t * 4f - 4f;

            case SmoothingType.SnappySpring:
                // Slightly sharpened peaks for energetic running gaits
                return Mathf.Sign(rawSine) * Mathf.Pow(Mathf.Abs(rawSine), 0.75f);

            case SmoothingType.Smooth:
            default:
                return rawSine;
        }
    }

    private void ApplyBoneRotation(int index, Quaternion targetRot, SmoothingType mode, float speed, float cycleSpeedMult, float dt)
    {
        if (speed <= 0f)
        {
            currentLocalRotations[index] = targetRot;
            legBones[index].localRotation = targetRot;
            return;
        }

        float effectiveSpeed = speed * Mathf.Max(1f, cycleSpeedMult * 0.5f);

        if (mode == SmoothingType.Linear)
        {
            float maxDegreesDelta = effectiveSpeed * 15f * dt;
            currentLocalRotations[index] = Quaternion.RotateTowards(currentLocalRotations[index], targetRot, maxDegreesDelta);
        }
        else
        {
            float t = 1f - Mathf.Exp(-effectiveSpeed * dt);
            currentLocalRotations[index] = Quaternion.Slerp(currentLocalRotations[index], targetRot, t);
        }

        legBones[index].localRotation = currentLocalRotations[index];
    }

    private SharedFKClock GetAndUpdateSharedClock(float currentCycleSpeed, float dt)
    {
        if (rigRoot == null)
            rigRoot = transform.root != null ? transform.root : transform;

        if (!s_rigClocks.TryGetValue(rigRoot, out SharedFKClock clock))
        {
            clock = new SharedFKClock();
            s_rigClocks[rigRoot] = clock;
        }

        if (clock.lastUpdatedFrame != Time.frameCount)
        {
            if (clock.lastUpdatedFrame != -1 && Time.frameCount - clock.lastUpdatedFrame > 1)
            {
                clock.elapsedActiveTime = 0f;
                clock.accumulatedCycles = 0f;
            }

            clock.elapsedActiveTime += dt;
            clock.accumulatedCycles += dt * currentCycleSpeed;
            clock.lastUpdatedFrame = Time.frameCount;
        }

        return clock;
    }

    private float CalculateNormalizedPhase(SharedFKClock clock, float activeCycleSpeed)
    {
        float singleStrideDuration = 1f / activeCycleSpeed;
        float totalPeriodSeconds = singleStrideDuration + Mathf.Max(0f, stepRestDelay);

        if (timingOffsetMode == TimingOffsetMode.NormalizedPhase)
        {
            if (stepRestDelay <= 0.0001f)
            {
                return Mathf.Repeat(clock.accumulatedCycles - timingOffset, 1f);
            }

            float offsetTime = clock.elapsedActiveTime - (timingOffset * totalPeriodSeconds);
            float timeInPeriod = Mathf.Repeat(offsetTime, totalPeriodSeconds);
            return Mathf.Clamp01(timeInPeriod / singleStrideDuration);
        }
        else
        {
            float delayedTime = clock.elapsedActiveTime - timingOffset;
            if (stepRestDelay <= 0.0001f)
            {
                return Mathf.Repeat(delayedTime * activeCycleSpeed, 1f);
            }

            float timeInPeriod = Mathf.Repeat(delayedTime, totalPeriodSeconds);
            return Mathf.Clamp01(timeInPeriod / singleStrideDuration);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Transform followBone = GetActiveFollowBone();
        if (!followDParabola || !dParabola.showGizmo || followBone == null)
            return;

        Transform rootRef = transform.root != null ? transform.root : transform;
        Transform hipBone = (legBones != null && legBones.Count > 0 && legBones[0] != null) ? legBones[0] : transform;
        Vector3 baseWorldPos;

        if (dParabola.customCenterTransform != null)
        {
            baseWorldPos = dParabola.customCenterTransform.position;
        }
        else if (Application.isPlaying && hipBone != null)
        {
            baseWorldPos = rootRef.TransformPoint((Vector2)rootRef.InverseTransformPoint(hipBone.position) + initialPawOffsetInRoot);
        }
        else
        {
            baseWorldPos = followBone.position;
        }

        Vector3 centerWorld = baseWorldPos + rootRef.TransformVector(new Vector3(dParabola.positionOffset.x, dParabola.positionOffset.y, 0f));

        GaitPreset activePreset = isRunning ? runningPreset : movingPreset;
        float strideMult = (!isStill && (isMoving || isRunning) && activePreset.dParabolaStrideMultiplier > 0.0001f) ? activePreset.dParabolaStrideMultiplier : 1f;
        float heightMult = (!isStill && (isMoving || isRunning) && activePreset.dParabolaHeightMultiplier > 0.0001f) ? activePreset.dParabolaHeightMultiplier : 1f;
        float effectiveStride = dParabola.strideLength * strideMult;
        float effectiveHeight = dParabola.stepHeight * heightMult;

        float dir = invertSwingDirection ? -1f : 1f;
        float bias = Mathf.Clamp(dParabola.apexBias, 0.2f, 0.8f);
        float rotRad = dParabola.pathRotationAngle * Mathf.Deg2Rad;

        Gizmos.color = Color.green;
        int segments = 24;
        Vector2 startLocal = RotateVector2(new Vector2(-effectiveStride * dir, 0f), rotRad);
        Vector3 prevPoint = centerWorld + rootRef.TransformVector(new Vector3(startLocal.x, startLocal.y, 0f));
        Vector3 startPoint = prevPoint;

        for (int i = 1; i <= segments; i++)
        {
            float u = i / (float)segments;
            float x = Mathf.Lerp(-effectiveStride, effectiveStride, u) * dir;
            float biasedU = u < bias ? (u / bias) * 0.5f : 0.5f + ((u - bias) / (1f - bias)) * 0.5f;
            float y = 4f * biasedU * (1f - biasedU) * effectiveHeight;

            Vector2 ptLocal = RotateVector2(new Vector2(x, y), rotRad);
            Vector3 nextPoint = centerWorld + rootRef.TransformVector(new Vector3(ptLocal.x, ptLocal.y, 0f));
            Gizmos.DrawLine(prevPoint, nextPoint);
            prevPoint = nextPoint;
        }

        // Draw flat bottom of the 'D'
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(prevPoint, startPoint);

        // Draw center anchor point of the D-Parabola
        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(centerWorld, 0.03f);

        if (Application.isPlaying)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(lastParabolaTargetWorld, 0.05f);
        }
    }
}