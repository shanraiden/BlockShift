using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class SpriteShadowCaster2D : MonoBehaviour
{
    public enum ShadowType { BlackEternal, WhiteSolidIce }

    [Header("Projection Settings")]
    public Vector2 baseLightDirection = new Vector2(0f, -1f); // Neutral vertical bias

    [Header("Transparency / Alpha Settings")]
    [Tooltip("Pixels with alpha at or below this threshold are treated as transparent and will not cast shadows.")]
    [Range(0.01f, 1f)]
    public float alphaThreshold = 0.1f;

    [Tooltip("Maximum grid resolution used to sample non-transparent pixels from the sprite.")]
    [Range(16, 256)]
    public int maxPixelSampleResolution = 128;

    [Header("Width Scaling Settings")]
    [Range(0.1f, 3f)]
    public float shadowWidthFactor = 1.0f;

    [Header("Tag & Single Sprite Object Interaction")]
    [Tooltip("If true, this shadow caster reacts to a nearby Sprite GameObject matching one of the Interacting Tags.")]
    public bool affectedByPlayerPosition = true;

    [Tooltip("Only GameObjects with one of these specific tags can interact with this shadow caster.")]
    public List<string> interactingTags = new List<string> { "Player" };

    [Tooltip("If true, only tagged GameObjects that have a SpriteRenderer (on themselves, parent, or children) can influence the shadow.")]
    public bool requireSpriteRendererOnInfluencer = true;

    [Tooltip("If true, once 1 valid Sprite object enters the Detection Radius, no other Sprite object can influence this shadow caster until the active one leaves the radius. If false, only the single closest valid Sprite object influences it at a time.")]
    public bool lockActiveInfluencerUntilExit = true;

    public bool shadowDirectionPull;
    [Range(0f, 1f)]
    public float playerRepulsionStrength = 1.0f;

    [Header("Dynamic Scaling & Detection Radius Settings")]
    public float minShadowLength = 1f;
    public float maxShadowLength = 5f;
    [Tooltip("Detection Radius within which a valid tagged Sprite object can influence this shadow.")]
    public float maxInteractionDistance = 6f;

    [Header("Smoothing Settings")]
    public float transitionSpeed = 10f;

    [Header("Shadow Collider & Solidification Settings")]
    [Tooltip("WhiteSolidIce = Hard PolygonCollider2D (isTrigger = false, cannot be bypassed). BlackEternal = Trigger PolygonCollider2D (isTrigger = true, specific tags float inside).")]
    public ShadowType currentShadowType = ShadowType.BlackEternal;
    public Material blackShadowMaterial;
    public Material whiteShadowMaterial;

    [Tooltip("If true, maintains a live PolygonCollider2D on this shadow GameObject updated with the projected shadow contour.")]
    public bool enableRealtimeShadowCollider = true;

    [Header("BlackEternal Trigger Float & Particle Settings")]
    [Tooltip("Prefab containing a ParticleSystem that activates and floats inside a BlackEternal shadow trigger when a tagged GameObject enters.")]
    public GameObject shadowFloatParticlePrefab;

    [Tooltip("Specific tags allowed to enter and float inside BlackEternal shadows (if empty, uses Interacting Tags above).")]
    public List<string> shadowFloatTags = new List<string> { "Player" };

    [Tooltip("Upward floating speed applied inside BlackEternal shadow during Trigger Stay.")]
    public float shadowFloatUpwardSpeed = 2.5f;

    [Tooltip("Horizontal/Vertical steering speed (Keyboard/Gamepad) while floating as particles inside BlackEternal shadow.")]
    public float shadowFloatSteerSpeed = 5.0f;

    [Tooltip("Velocity damping while floating inside the BlackEternal shadow.")]
    public float shadowFloatDamping = 4.0f;

    [Tooltip("Cooldown (seconds) after exiting a BlackEternal shadow before the reactivated GameObject can re-enter.")]
    public float shadowExitReentryCooldown = 0.25f;

    [Header("BlackEternal Exit Force Settings (Outward from ShadowCaster)")]
    [Tooltip("If true, applies an outward exit force to the Particle Player (from this ShadowCaster toward the Player) before re-enabling the Base Player.")]
    public bool applyExitForce = true;

    [Tooltip("Time (in seconds) to wait after applying the outward exit force on the Particle Player before re-enabling the Base Player.")]
    [Min(0f)]
    public float exitEnableDelay = 0.25f;

    [Tooltip("Fallback local direction used only if the Particle Player is at the exact same position as the ShadowCaster.")]
    public Vector2 localExitForceDirection = new Vector2(0f, 1f);

    [Tooltip("Magnitude of the outward exit force applied to the Particle Player when leaving the BlackEternal shadow.")]
    public float exitForceMagnitude = 8f;

    [Tooltip("How the exit force is applied to the Particle Player's Rigidbody2D.")]
    public ForceMode2D exitForceMode = ForceMode2D.Impulse;

    [Tooltip("If true, clears existing Particle Player velocity before applying the outward exit force.")]
    public bool resetVelocityBeforeExitForce = true;

    [Header("BlackEternal Floating Input (New Input System)")]
    [Tooltip("Optional InputActionReference (Vector2) from your Input Actions asset for steering inside BlackEternal shadows (e.g. D-Pad / Move).")]
    public InputActionReference shadowFloatMoveActionReference;

    [Tooltip("Customizable inline InputAction (Vector2) for steering inside BlackEternal shadows (defaults to Gamepad D-Pad).")]
    public InputAction shadowFloatMoveAction = new InputAction(
        name: "ShadowFloatMove",
        type: InputActionType.Value,
        binding: "<Gamepad>/dpad",
        expectedControlType: "Vector2"
    );

    [Header("Gizmo Visualization Settings")]
    [Tooltip("Draw the Detection Radius and active influencer Gizmos in the Scene View.")]
    public bool showGizmos = true;

    [Tooltip("If false, the Detection Radius Gizmo is always visible even when this GameObject is not selected.")]
    public bool drawGizmosOnlyWhenSelected = false;

    [Range(16, 96)]
    public int gizmoCircleSegments = 48;

    public Color idleDetectionRadiusColor = new Color(0.2f, 0.75f, 1f, 0.85f);
    public Color activeDetectionRadiusColor = new Color(1f, 0.85f, 0.15f, 0.95f);
    public Color influencerLinkColor = new Color(0.2f, 0.95f, 0.4f, 0.95f);

    [Header("Status (Read-Only)")]
    [SerializeField] private GameObject currentInfluencingObject;
    [SerializeField] private GameObject currentFloatingDisabledObject;
    [SerializeField] private GameObject activeParticleProxyInstance;

    // Component and Runtime References
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private PolygonCollider2D shadowPolygonCollider;
    private Mesh currentMesh;
    private SpriteRenderer spriteRenderer;
    private Transform currentInfluencingTransform;

    private Vector2 currentShadowDir;
    private float currentLength;
    private bool isInPlayerRange;
    public bool IsInPlayerRange => isInPlayerRange;
    public GameObject CurrentInfluencingObject => currentInfluencingObject;
    [HideInInspector] public bool isShadowFrozen;

    // Cached Non-Transparent Pixel Geometry (in SpriteRenderer local space)
    private Sprite cachedSprite;
    private float cachedAlphaThreshold = -1f;
    private int cachedSampleResolution = -1;
    private Vector2[] baseOpaqueVertices;
    private int[] baseOpaqueTriangles;
    private int[] flippedOpaqueTriangles;
    private List<Vector2[]> baseOpaqueContours = new List<Vector2[]>();
    private Vector2 opaqueCenter;
    private Vector3[] runtimeMeshVertices;
    private Vector2[] worldScaledBuffer;
    private bool geometryDirty = true;
    private float lastOrientationSign = 0f;
    private Vector2 lastColliderShadowDir;
    private float lastColliderShadowLength = -1f;
    private float lastColliderWidthFactor = -1f;
    private ShadowType lastConfiguredShadowType;

    // BlackSolid Floating State Tracking
    private class FloatingShadowSession
    {
        public GameObject originalObject;
        public Vector2 storedVelocity;
        public float originalGravityScale;
        public GameObject particleProxyObject;
        public Rigidbody2D proxyRigidbody;
        public Collider2D proxyCollider;
        public PolygonCollider2D triggerCollider;
        public ParticleSystem[] particleSystems;
        public float enterTime;
        public bool isExiting;
        public float exitEnableAtTime;
        public Vector2 exitForceDirection;
    }

    private readonly Dictionary<GameObject, FloatingShadowSession> m_proxyToSession = new Dictionary<GameObject, FloatingShadowSession>();
    private readonly Dictionary<GameObject, float> m_reentryCooldownUntil = new Dictionary<GameObject, float>();

    private void OnValidate()
    {
        if (interactingTags == null || interactingTags.Count == 0)
        {
            interactingTags = new List<string> { "Player" };
        }
        if (shadowFloatTags == null || shadowFloatTags.Count == 0)
        {
            shadowFloatTags = new List<string>(interactingTags);
        }
        UpdateMaterial();
        SyncShadowColliderMode();
    }

    void Awake()
    {
        spriteRenderer = GetComponentInParent<SpriteRenderer>();

        if (!TryGetComponent<MeshFilter>(out meshFilter))
            meshFilter = gameObject.AddComponent<MeshFilter>();

        if (!TryGetComponent<MeshRenderer>(out meshRenderer))
            meshRenderer = gameObject.AddComponent<MeshRenderer>();

        if (enableRealtimeShadowCollider && !TryGetComponent<PolygonCollider2D>(out shadowPolygonCollider))
            shadowPolygonCollider = gameObject.AddComponent<PolygonCollider2D>();

        currentMesh = new Mesh();
        currentMesh.MarkDynamic();
        meshFilter.mesh = currentMesh;

        // Set initial preview material & collider mode based on type
        UpdateMaterial();
        SyncShadowColliderMode();

        if (interactingTags == null || interactingTags.Count == 0)
        {
            interactingTags = new List<string> { "Player" };
        }
        if (shadowFloatTags == null || shadowFloatTags.Count == 0)
        {
            shadowFloatTags = new List<string>(interactingTags);
        }

        float objectScale = Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y));
        currentShadowDir = baseLightDirection.sqrMagnitude > 0.0001f ? baseLightDirection.normalized : Vector2.down;
        currentLength = minShadowLength * objectScale;

        EnsureOpaqueGeometryCached();
    }

    void Update()
    {
        if (lastConfiguredShadowType != currentShadowType)
        {
            UpdateMaterial();
            SyncShadowColliderMode();
        }

        UpdateSmoothedShadowValues();
        UpdateRealTimePreview();
    }

    private void FixedUpdate()
    {
        if (m_proxyToSession.Count == 0)
            return;

        List<GameObject> beginExitProxies = null;
        List<GameObject> finalizeProxies = null;

        foreach (var kvp in m_proxyToSession)
        {
            GameObject proxyObj = kvp.Key;
            FloatingShadowSession session = kvp.Value;
            if (proxyObj == null || session == null)
                continue;

            if (currentShadowType != ShadowType.BlackEternal)
            {
                if (finalizeProxies == null) finalizeProxies = new List<GameObject>();
                finalizeProxies.Add(proxyObj);
                continue;
            }

            if (session.isExiting)
            {
                // Let the Particle Player travel under the outward exit force while waiting to re-enable Base Player
                SyncExitingSession(session);

                if (Time.time >= session.exitEnableAtTime)
                {
                    if (finalizeProxies == null) finalizeProxies = new List<GameObject>();
                    finalizeProxies.Add(proxyObj);
                }
                continue;
            }

            // Drive floating movement and particle playback deterministically every FixedUpdate
            UpdateFloatingSessionMotion(session);

            // Give newly spawned proxy a brief grace window for physics transforms to sync
            if (Time.time - session.enterTime < 0.12f)
                continue;

            if (!IsSessionStillOverlappingShadow(session, session.triggerCollider != null ? session.triggerCollider : shadowPolygonCollider))
            {
                if (beginExitProxies == null) beginExitProxies = new List<GameObject>();
                beginExitProxies.Add(proxyObj);
            }
        }

        if (beginExitProxies != null)
        {
            for (int i = 0; i < beginExitProxies.Count; i++)
            {
                BeginExitFloatingSessionForProxy(beginExitProxies[i]);
            }
        }

        if (finalizeProxies != null)
        {
            for (int i = 0; i < finalizeProxies.Count; i++)
            {
                EndFloatingSessionForProxy(finalizeProxies[i]);
            }
        }
    }

    private void OnEnable()
    {
        if (shadowFloatMoveActionReference != null && shadowFloatMoveActionReference.action != null)
        {
            shadowFloatMoveActionReference.action.Enable();
        }

        if (shadowFloatMoveAction != null)
        {
            if (shadowFloatMoveAction.bindings.Count == 0)
            {
                shadowFloatMoveAction.AddBinding("<Gamepad>/dpad");
            }
            shadowFloatMoveAction.Enable();
        }
    }

    private void OnDisable()
    {
        RestoreAllFloatingSessions();

        if (shadowFloatMoveAction != null)
        {
            shadowFloatMoveAction.Disable();
        }
    }

    void UpdateMaterial()
    {
        if (meshRenderer == null) return;

        if (currentShadowType == ShadowType.BlackEternal && blackShadowMaterial != null)
            meshRenderer.sharedMaterial = blackShadowMaterial;
        else if (currentShadowType == ShadowType.WhiteSolidIce && whiteShadowMaterial != null)
            meshRenderer.sharedMaterial = whiteShadowMaterial;
    }

    private void SyncShadowColliderMode()
    {
        lastConfiguredShadowType = currentShadowType;

        if (!enableRealtimeShadowCollider)
        {
            if (shadowPolygonCollider != null)
                shadowPolygonCollider.enabled = false;
            return;
        }

        if (shadowPolygonCollider == null)
            TryGetComponent(out shadowPolygonCollider);

        if (shadowPolygonCollider == null)
            return;

        shadowPolygonCollider.enabled = true;
        shadowPolygonCollider.offset = Vector2.zero;

        // WhiteSolidIce -> Hard PolygonCollider2D (cannot be bypassed)
        // BlackEternal -> Trigger PolygonCollider2D (specific tags float inside)
        if (currentShadowType == ShadowType.WhiteSolidIce)
        {
            shadowPolygonCollider.isTrigger = false;
            RestoreAllFloatingSessions();
        }
        else if (currentShadowType == ShadowType.BlackEternal)
        {
            shadowPolygonCollider.isTrigger = true;
        }
    }

    void UpdateSmoothedShadowValues()
    {
        if (isShadowFrozen) return;

        float objectScale = Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y));
        Vector2 targetDirection = baseLightDirection.sqrMagnitude > 0.0001f ? baseLightDirection.normalized : currentShadowDir;
        float targetLength = minShadowLength * objectScale;

        if (affectedByPlayerPosition)
        {
            currentInfluencingTransform = ResolveSingleInfluencingSpriteObject(out float distanceToInfluencer);
            currentInfluencingObject = currentInfluencingTransform != null ? currentInfluencingTransform.gameObject : null;

            if (currentInfluencingTransform != null && distanceToInfluencer <= maxInteractionDistance && distanceToInfluencer > 0.0001f)
            {
                isInPlayerRange = true;
                Vector2 casterToInfluencer = (Vector2)currentInfluencingTransform.position - (Vector2)transform.position;
                Vector2 baseDir = baseLightDirection.sqrMagnitude > 0.0001f ? baseLightDirection.normalized : Vector2.zero;
                Vector2 influencerDir = casterToInfluencer.normalized * (shadowDirectionPull ? 1 : -1);

                Vector2 combinedDir = baseDir + (influencerDir * playerRepulsionStrength);
                if (combinedDir.sqrMagnitude > 0.0001f)
                    targetDirection = combinedDir.normalized;

                float distanceFactor = Mathf.Clamp01(1f - (distanceToInfluencer / maxInteractionDistance));
                targetLength = Mathf.Lerp(minShadowLength * objectScale, maxShadowLength * objectScale, distanceFactor);
            }
            else
            {
                isInPlayerRange = false;
            }
        }
        else
        {
            currentInfluencingTransform = null;
            currentInfluencingObject = null;
            isInPlayerRange = false;
        }

        currentShadowDir = Vector2.Lerp(currentShadowDir, targetDirection, Time.deltaTime * transitionSpeed);
        currentLength = Mathf.Lerp(currentLength, targetLength, Time.deltaTime * transitionSpeed);
    }

    /// <summary>
    /// Finds at most 1 valid tagged Sprite object within maxInteractionDistance to influence this shadow caster.
    /// </summary>
    private Transform ResolveSingleInfluencingSpriteObject(out float distanceOut)
    {
        distanceOut = float.MaxValue;
        Vector2 center = transform.position;
        float radius = Mathf.Max(0f, maxInteractionDistance);
        float radiusSqr = radius * radius;

        // 1. If lockActiveInfluencerUntilExit is true and our current influencer is still valid and inside the radius, keep it exclusively
        if (lockActiveInfluencerUntilExit && IsValidInfluencerCandidate(currentInfluencingTransform))
        {
            float currentSqrDist = ((Vector2)currentInfluencingTransform.position - center).sqrMagnitude;
            if (currentSqrDist <= radiusSqr)
            {
                distanceOut = Mathf.Sqrt(currentSqrDist);
                return currentInfluencingTransform;
            }
        }

        Transform bestCandidate = null;
        float closestSqrDist = radiusSqr;

        // 2. Search tagged GameObjects for each allowed tag in interactingTags
        if (interactingTags != null)
        {
            for (int t = 0; t < interactingTags.Count; t++)
            {
                string tagName = interactingTags[t];
                if (string.IsNullOrWhiteSpace(tagName))
                    continue;

                GameObject[] taggedObjects;
                try
                {
                    taggedObjects = GameObject.FindGameObjectsWithTag(tagName);
                }
                catch (UnityException)
                {
                    // Tag does not exist in TagManager yet
                    continue;
                }

                if (taggedObjects == null) continue;

                for (int i = 0; i < taggedObjects.Length; i++)
                {
                    GameObject obj = taggedObjects[i];
                    if (obj == null) continue;

                    Transform candidateTransform = GetCanonicalSpriteObjectTransform(obj.transform);
                    if (!IsValidInfluencerCandidate(candidateTransform))
                        continue;

                    float sqrDist = ((Vector2)candidateTransform.position - center).sqrMagnitude;
                    if (sqrDist <= closestSqrDist)
                    {
                        closestSqrDist = sqrDist;
                        bestCandidate = candidateTransform;
                    }
                }
            }
        }

        if (bestCandidate != null)
        {
            distanceOut = Mathf.Sqrt(closestSqrDist);
        }

        return bestCandidate;
    }

    private Transform GetCanonicalSpriteObjectTransform(Transform candidate)
    {
        if (candidate == null) return null;

        if (candidate.TryGetComponent<SpriteRenderer>(out _))
            return candidate;

        SpriteRenderer parentSr = candidate.GetComponentInParent<SpriteRenderer>();
        if (parentSr != null)
            return parentSr.transform;

        SpriteRenderer childSr = candidate.GetComponentInChildren<SpriteRenderer>();
        if (childSr != null)
            return childSr.transform;

        return candidate;
    }

    private bool IsValidInfluencerCandidate(Transform candidate)
    {
        if (candidate == null)
            return false;

        bool isFloatingInThisShadow = IsObjectInFloatingSession(candidate.gameObject);
        if (!candidate.gameObject.activeInHierarchy && !isFloatingInThisShadow)
            return false;

        // Prevent this shadow caster from influencing itself
        if (candidate == transform || candidate.IsChildOf(transform) || transform.IsChildOf(candidate))
            return false;

        if (!HasAllowedInteractingTag(candidate))
            return false;

        if (requireSpriteRendererOnInfluencer)
        {
            bool hasSprite = candidate.GetComponent<SpriteRenderer>() != null ||
                             candidate.GetComponentInChildren<SpriteRenderer>() != null ||
                             candidate.GetComponentInParent<SpriteRenderer>() != null;
            if (!hasSprite)
                return false;
        }

        return true;
    }

    private bool IsObjectInFloatingSession(GameObject obj)
    {
        if (obj == null || m_proxyToSession.Count == 0)
            return false;

        Transform objRoot = obj.transform.root;
        foreach (var kvp in m_proxyToSession)
        {
            GameObject orig = kvp.Value.originalObject;
            if (orig == null) continue;

            if (orig == obj || orig.transform.IsChildOf(obj.transform) || obj.transform.IsChildOf(orig.transform) ||
                (objRoot != null && orig.transform.root == objRoot))
            {
                return true;
            }
        }

        return false;
    }

    private bool HasAllowedInteractingTag(Transform candidate)
    {
        if (candidate == null || interactingTags == null || interactingTags.Count == 0)
            return false;

        Transform root = candidate.root;

        for (int i = 0; i < interactingTags.Count; i++)
        {
            string allowedTag = interactingTags[i];
            if (string.IsNullOrWhiteSpace(allowedTag))
                continue;

            if (candidate.gameObject.tag == allowedTag || (root != null && root.gameObject.tag == allowedTag))
                return true;
        }

        return false;
    }

    void UpdateRealTimePreview()
    {
        EnsureOpaqueGeometryCached();

        if (baseOpaqueVertices == null || baseOpaqueVertices.Length < 3 || baseOpaqueTriangles == null || baseOpaqueTriangles.Length < 3)
        {
            meshRenderer.enabled = false;
            if (shadowPolygonCollider != null)
                shadowPolygonCollider.enabled = false;
            return;
        }

        meshRenderer.enabled = true;

        int vertCount = baseOpaqueVertices.Length;
        if (runtimeMeshVertices == null || runtimeMeshVertices.Length != vertCount)
        {
            runtimeMeshVertices = new Vector3[vertCount];
        }

        TransformProjectedPoints(baseOpaqueVertices, runtimeMeshVertices, transform);

        float currentOrientationSign = GetOrientationSign(transform);
        bool topologyChanged = geometryDirty || currentMesh.vertexCount != vertCount || currentOrientationSign != lastOrientationSign;
        if (topologyChanged)
        {
            currentMesh.Clear();
            currentMesh.vertices = runtimeMeshVertices;
            currentMesh.triangles = GetOrientedTriangles(transform);
            geometryDirty = false;
            lastOrientationSign = currentOrientationSign;
        }
        else
        {
            currentMesh.vertices = runtimeMeshVertices;
        }

        currentMesh.RecalculateNormals();
        currentMesh.RecalculateBounds();

        // Update the live PolygonCollider2D on this shadow when shape/direction/length changes
        if (enableRealtimeShadowCollider)
        {
            if (shadowPolygonCollider == null && !TryGetComponent(out shadowPolygonCollider))
                shadowPolygonCollider = gameObject.AddComponent<PolygonCollider2D>();

            SyncShadowColliderMode();

            bool colliderShapeChanged = topologyChanged ||
                                        shadowPolygonCollider.pathCount != baseOpaqueContours.Count ||
                                        Vector2.Distance(lastColliderShadowDir, currentShadowDir) > 0.001f ||
                                        Mathf.Abs(lastColliderShadowLength - currentLength) > 0.001f ||
                                        Mathf.Abs(lastColliderWidthFactor - shadowWidthFactor) > 0.001f;

            if (colliderShapeChanged)
            {
                ApplyProjectedContoursToCollider(shadowPolygonCollider, transform);
                lastColliderShadowDir = currentShadowDir;
                lastColliderShadowLength = currentLength;
                lastColliderWidthFactor = shadowWidthFactor;
            }
        }
    }

    private void ApplyProjectedContoursToCollider(PolygonCollider2D targetCollider, Transform targetTransform)
    {
        if (targetCollider == null) return;

        targetCollider.pathCount = baseOpaqueContours.Count;
        for (int c = 0; c < baseOpaqueContours.Count; c++)
        {
            Vector2[] contour = baseOpaqueContours[c];
            Vector3[] projectedLocal3D = new Vector3[contour.Length];
            TransformProjectedPoints(contour, projectedLocal3D, targetTransform);

            Vector2[] colliderPath = new Vector2[contour.Length];
            for (int i = 0; i < contour.Length; i++)
            {
                colliderPath[i] = new Vector2(projectedLocal3D[i].x, projectedLocal3D[i].y);
            }
            targetCollider.SetPath(c, colliderPath);
        }
    }

    /// <summary>
    /// Returns projected world-space vertices of the outer non-transparent contour.
    /// </summary>
    public Vector2[] GetProjectedShadowVertices()
    {
        EnsureOpaqueGeometryCached();
        if (baseOpaqueVertices == null || baseOpaqueVertices.Length == 0) return null;

        Vector2[] sourcePoints = baseOpaqueContours.Count > 0 ? baseOpaqueContours[0] : baseOpaqueVertices;
        Vector2[] worldPoints = new Vector2[sourcePoints.Length];
        TransformProjectedWorldPoints(sourcePoints, worldPoints);
        return worldPoints;
    }

    public void SolidifyShadow()
    {
        EnsureOpaqueGeometryCached();
        if (baseOpaqueVertices == null || baseOpaqueVertices.Length < 3 || baseOpaqueTriangles == null || baseOpaqueTriangles.Length < 3)
            return;

        GameObject solidPlatform = new GameObject($"SolidifiedShadow_{currentShadowType}");
        solidPlatform.transform.position = transform.position;
        solidPlatform.transform.rotation = transform.rotation;

        MeshFilter mf = solidPlatform.AddComponent<MeshFilter>();
        MeshRenderer mr = solidPlatform.AddComponent<MeshRenderer>();
        PolygonCollider2D polyCollider = solidPlatform.AddComponent<PolygonCollider2D>();

        // WhiteSolidIce -> Hard PolygonCollider2D (cannot be bypassed)
        // BlackEternal -> Trigger PolygonCollider2D (specific tags float inside with Particle System Prefab)
        if (currentShadowType == ShadowType.WhiteSolidIce)
        {
            solidPlatform.tag = "Ground";
            int groundLayer = LayerMask.NameToLayer("Ground");
            if (groundLayer != -1) solidPlatform.layer = groundLayer;
            polyCollider.isTrigger = false;
            if (whiteShadowMaterial != null) mr.material = whiteShadowMaterial;
        }
        else if (currentShadowType == ShadowType.BlackEternal)
        {
            solidPlatform.tag = "EtherealZone";
            polyCollider.isTrigger = true;
            if (blackShadowMaterial != null) mr.material = blackShadowMaterial;

            // Attach a SpriteShadowCaster2D trigger forwarder so baked BlackEternal shadows also support floating & particle swap
            ShadowFloatTriggerForwarder2D forwarder = solidPlatform.AddComponent<ShadowFloatTriggerForwarder2D>();
            forwarder.Initialize(this, polyCollider);
        }

        Vector3[] localVertices = new Vector3[baseOpaqueVertices.Length];
        TransformProjectedPoints(baseOpaqueVertices, localVertices, solidPlatform.transform);

        Mesh solidMesh = new Mesh();
        solidMesh.vertices = localVertices;
        solidMesh.triangles = GetOrientedTriangles(solidPlatform.transform);
        solidMesh.RecalculateNormals();
        solidMesh.RecalculateBounds();
        mf.mesh = solidMesh;

        // Apply only non-transparent pixel contours (including holes and separate islands) to PolygonCollider2D
        ApplyProjectedContoursToCollider(polyCollider, solidPlatform.transform);

        Debug.Log($"[ShadowBound] Successfully baked {currentShadowType} shadow platform from non-transparent pixels!");
    }

    // ========================================================================================
    // BlackEternal Trigger Floating & Particle System Prefab Swap Logic
    // ========================================================================================

    private void OnTriggerEnter2D(Collider2D other)
    {
        HandleShadowTriggerEnter(other, shadowPolygonCollider);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        HandleShadowTriggerStay(other);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        HandleShadowTriggerExit(other, shadowPolygonCollider);
    }

    internal void HandleShadowTriggerEnter(Collider2D other, PolygonCollider2D activeTriggerCollider)
    {
        if (currentShadowType != ShadowType.BlackEternal || other == null)
            return;

        GameObject candidateObj = ResolveFloatingTargetObject(other);
        if (candidateObj == null)
            return;

        // Ignore if this collider belongs to an already-active particle proxy
        if (m_proxyToSession.ContainsKey(other.gameObject) || m_proxyToSession.ContainsKey(candidateObj))
            return;

        // Ignore if candidateObj is on brief exit cooldown
        if (m_reentryCooldownUntil.TryGetValue(candidateObj, out float cooldownEnd) && Time.time < cooldownEnd)
            return;

        // Ignore if candidateObj is already floating in an active session
        foreach (var kvp in m_proxyToSession)
        {
            if (kvp.Value.originalObject == candidateObj)
                return;
        }

        StartFloatingSession(candidateObj, other, activeTriggerCollider != null ? activeTriggerCollider : shadowPolygonCollider);
    }

    internal void HandleShadowTriggerStay(Collider2D other)
    {
        // Floating motion and particle playback are driven deterministically in FixedUpdate()
        // so PolygonCollider2D.SetPath() rebuilds cannot drop or double-apply movement.
    }

    private void UpdateFloatingSessionMotion(FloatingShadowSession session)
    {
        if (session == null)
            return;

        // Keep Particle Systems playing while inside BlackEternal shadow
        if (session.particleSystems != null)
        {
            for (int i = 0; i < session.particleSystems.Length; i++)
            {
                ParticleSystem ps = session.particleSystems[i];
                if (ps != null && !ps.isPlaying)
                {
                    ps.Play(true);
                }
            }
        }

        // Float the Particle Prefab inside the BlackEternal shadow
        if (session.proxyRigidbody != null)
        {
            Vector2 moveInput = ReadFloatingSteerInput();
            Vector2 targetVel;

            if (moveInput.sqrMagnitude > 0.01f)
            {
                targetVel = new Vector2(
                    moveInput.x * shadowFloatSteerSpeed,
                    moveInput.y * shadowFloatSteerSpeed + shadowFloatUpwardSpeed * 0.35f
                );
            }
            else
            {
                targetVel = new Vector2(0f, shadowFloatUpwardSpeed);
            }

            session.proxyRigidbody.linearVelocity = Vector2.Lerp(
                session.proxyRigidbody.linearVelocity,
                targetVel,
                1f - Mathf.Exp(-Mathf.Max(1f, shadowFloatDamping) * Time.fixedDeltaTime)
            );
        }
        else if (session.particleProxyObject != null)
        {
            Vector2 moveInput = ReadFloatingSteerInput();
            Vector3 delta = new Vector3(
                moveInput.x * shadowFloatSteerSpeed,
                (moveInput.y * shadowFloatSteerSpeed) + shadowFloatUpwardSpeed,
                0f
            ) * Time.fixedDeltaTime;
            session.particleProxyObject.transform.position += delta;
        }

        // Keep the disabled original GameObject's position synced to the floating Particle System Prefab
        if (session.originalObject != null && session.particleProxyObject != null)
        {
            session.originalObject.transform.position = session.particleProxyObject.transform.position;
        }
    }

    private bool IsSessionStillOverlappingShadow(FloatingShadowSession session, PolygonCollider2D triggerCol)
    {
        if (session == null || session.particleProxyObject == null || triggerCol == null || !triggerCol.enabled)
            return false;

        Vector2 proxyPos = session.particleProxyObject.transform.position;
        if (triggerCol.OverlapPoint(proxyPos))
            return true;

        if (session.proxyCollider != null && session.proxyCollider.enabled)
        {
            if (triggerCol.IsTouching(session.proxyCollider))
                return true;

            ColliderDistance2D dist = Physics2D.Distance(triggerCol, session.proxyCollider);
            if (dist.isValid && dist.isOverlapped)
                return true;
        }

        return false;
    }

    internal void HandleShadowTriggerExit(Collider2D other, PolygonCollider2D activeTriggerCollider)
    {
        if (other == null) return;

        GameObject keyObj = other.attachedRigidbody != null ? other.attachedRigidbody.gameObject : other.gameObject;
        GameObject matchedProxyKey = null;
        FloatingShadowSession session = null;

        if (m_proxyToSession.TryGetValue(keyObj, out session))
        {
            matchedProxyKey = keyObj;
        }
        else if (m_proxyToSession.TryGetValue(other.gameObject, out session))
        {
            matchedProxyKey = other.gameObject;
        }

        // Ignore the immediate OnTriggerExit2D fired by Unity when the original GameObject is disabled on Enter
        if (matchedProxyKey == null || session == null || session.isExiting)
            return;

        // Ignore exits during the initial entry grace window
        if (Time.time - session.enterTime < 0.12f)
            return;

        // Guard against false trigger exit caused by PolygonCollider2D.SetPath rebuilding while still inside the polygon
        PolygonCollider2D triggerToCheck = activeTriggerCollider != null ? activeTriggerCollider : session.triggerCollider;
        if (currentShadowType == ShadowType.BlackEternal && IsSessionStillOverlappingShadow(session, triggerToCheck))
            return;

        BeginExitFloatingSessionForProxy(matchedProxyKey);
    }

    private void StartFloatingSession(GameObject targetObj, Collider2D enteringCollider, PolygonCollider2D triggerCol)
    {
        if (targetObj == null) return;

        Rigidbody2D origRb = targetObj.GetComponent<Rigidbody2D>();
        Vector2 initialVel = origRb != null ? origRb.linearVelocity : Vector2.zero;
        float origGravity = origRb != null ? origRb.gravityScale : 1f;
        Vector3 enterPos = targetObj.transform.position;

        // 1. Disable the entering GameObject
        targetObj.SetActive(false);

        // 2. Activate or instantiate the Particle System Prefab at the entering position
        GameObject proxyObj;
        if (shadowFloatParticlePrefab != null)
        {
            // If shadowFloatParticlePrefab is a scene object that is currently inactive, activate it directly; if it's an asset prefab, instantiate it
            if (shadowFloatParticlePrefab.scene.IsValid())
            {
                proxyObj = shadowFloatParticlePrefab;
                proxyObj.transform.position = enterPos;
                proxyObj.SetActive(true);
            }
            else
            {
                proxyObj = Instantiate(shadowFloatParticlePrefab, enterPos, Quaternion.identity);
                proxyObj.SetActive(true);
            }
        }
        else
        {
            // Fallback runtime ParticleSystem if no prefab was assigned in the Inspector
            proxyObj = CreateFallbackParticleProxy(enterPos, targetObj.name);
        }

        // Ensure the Particle Prefab has a Rigidbody2D + Collider2D so Unity keeps firing trigger events and Physics2D.Distance works
        Rigidbody2D proxyRb = proxyObj.GetComponent<Rigidbody2D>();
        if (proxyRb == null)
        {
            proxyRb = proxyObj.AddComponent<Rigidbody2D>();
        }
        proxyRb.bodyType = RigidbodyType2D.Dynamic;
        proxyRb.gravityScale = 0f;
        proxyRb.sleepMode = RigidbodySleepMode2D.NeverSleep;
        proxyRb.constraints = RigidbodyConstraints2D.FreezeRotation;
        proxyRb.linearVelocity = new Vector2(initialVel.x * 0.5f, Mathf.Max(initialVel.y * 0.35f, shadowFloatUpwardSpeed));

        Collider2D proxyCol = proxyObj.GetComponent<Collider2D>();
        if (proxyCol == null)
        {
            CircleCollider2D circle = proxyObj.AddComponent<CircleCollider2D>();
            float approxRadius = 0.25f;
            if (enteringCollider != null)
            {
                Vector3 ext = enteringCollider.bounds.extents;
                approxRadius = Mathf.Clamp(Mathf.Min(ext.x, ext.y), 0.15f, 0.75f);
            }
            circle.radius = approxRadius;
            circle.offset = Vector2.zero;
            circle.isTrigger = true;
            proxyCol = circle;
        }
        else
        {
            proxyCol.enabled = true;
            proxyCol.offset = Vector2.zero;
            proxyCol.isTrigger = true;
        }

        ParticleSystem[] particleSystems = proxyObj.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particleSystems.Length; i++)
        {
            if (particleSystems[i] != null)
            {
                particleSystems[i].gameObject.SetActive(true);
                particleSystems[i].Play(true);
            }
        }

        FloatingShadowSession session = new FloatingShadowSession
        {
            originalObject = targetObj,
            storedVelocity = initialVel,
            originalGravityScale = origGravity,
            particleProxyObject = proxyObj,
            proxyRigidbody = proxyRb,
            proxyCollider = proxyCol,
            triggerCollider = triggerCol,
            particleSystems = particleSystems,
            enterTime = Time.time
        };

        m_proxyToSession[proxyObj] = session;
        currentFloatingDisabledObject = targetObj;
        activeParticleProxyInstance = proxyObj;
    }

    private void BeginExitFloatingSessionForProxy(GameObject proxyKey)
    {
        if (proxyKey == null || !m_proxyToSession.TryGetValue(proxyKey, out FloatingShadowSession session))
            return;

        if (session.isExiting)
            return;

        session.isExiting = true;
        session.exitEnableAtTime = Time.time + Mathf.Max(0f, exitEnableDelay);

        // Apply outward force on the Particle Player FIRST (direction: from ShadowCaster -> Particle Player)
        if (applyExitForce && !Mathf.Approximately(exitForceMagnitude, 0f) && session.particleProxyObject != null)
        {
            Vector3 proxyPos = session.particleProxyObject.transform.position;
            Vector2 fromShadowToPlayer = (Vector2)proxyPos - (Vector2)transform.position;
            Vector2 outwardDir = fromShadowToPlayer.sqrMagnitude > 0.0001f
                ? fromShadowToPlayer.normalized
                : (localExitForceDirection.sqrMagnitude > 0.0001f
                    ? ((Vector2)transform.TransformDirection(localExitForceDirection)).normalized
                    : Vector2.up);

            session.exitForceDirection = outwardDir;

            if (session.proxyRigidbody != null)
            {
                session.proxyRigidbody.WakeUp();
                if (resetVelocityBeforeExitForce)
                {
                    session.proxyRigidbody.linearVelocity = Vector2.zero;
                }
                session.proxyRigidbody.AddForce(outwardDir * exitForceMagnitude, exitForceMode);
            }
        }

        // If no wait delay is set, immediately re-enable the Base Player
        if (exitEnableDelay <= 0f)
        {
            EndFloatingSessionForProxy(proxyKey);
        }
    }

    private void SyncExitingSession(FloatingShadowSession session)
    {
        if (session == null)
            return;

        // Keep Particle Systems playing during the exit launch delay
        if (session.particleSystems != null)
        {
            for (int i = 0; i < session.particleSystems.Length; i++)
            {
                ParticleSystem ps = session.particleSystems[i];
                if (ps != null && !ps.isPlaying)
                {
                    ps.Play(true);
                }
            }
        }

        // If ForceMode2D.Force is selected, continue applying force during the launch window
        if (applyExitForce && exitForceMode == ForceMode2D.Force && !Mathf.Approximately(exitForceMagnitude, 0f) &&
            session.proxyRigidbody != null && session.exitForceDirection.sqrMagnitude > 0.0001f)
        {
            session.proxyRigidbody.AddForce(session.exitForceDirection * exitForceMagnitude, ForceMode2D.Force);
        }

        // Keep Base Player position synced to the flying Particle Player until it is re-enabled
        if (session.originalObject != null && session.particleProxyObject != null)
        {
            session.originalObject.transform.position = session.particleProxyObject.transform.position;
        }
    }

    private void EndFloatingSessionForProxy(GameObject proxyKey)
    {
        if (proxyKey == null || !m_proxyToSession.TryGetValue(proxyKey, out FloatingShadowSession session))
            return;

        m_proxyToSession.Remove(proxyKey);

        Vector3 exitPosition = session.particleProxyObject != null
            ? session.particleProxyObject.transform.position
            : (session.originalObject != null ? session.originalObject.transform.position : transform.position);

        Vector2 exitVelocity = session.proxyRigidbody != null
            ? session.proxyRigidbody.linearVelocity
            : session.storedVelocity;

        // 1. Deactivate or destroy the Particle System Prefab
        if (session.particleProxyObject != null)
        {
            if (session.particleSystems != null)
            {
                for (int i = 0; i < session.particleSystems.Length; i++)
                {
                    if (session.particleSystems[i] != null)
                        session.particleSystems[i].Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }
            }

            if (shadowFloatParticlePrefab != null && session.particleProxyObject == shadowFloatParticlePrefab)
            {
                session.particleProxyObject.SetActive(false);
            }
            else
            {
                session.particleProxyObject.SetActive(false);
                Destroy(session.particleProxyObject);
            }
        }

        // 2. Now re-enable the Base Player at the Particle Player's final position & velocity
        if (session.originalObject != null)
        {
            session.originalObject.transform.position = exitPosition;
            m_reentryCooldownUntil[session.originalObject] = Time.time + Mathf.Max(0.05f, shadowExitReentryCooldown);
            session.originalObject.SetActive(true);

            Rigidbody2D origRb = session.originalObject.GetComponent<Rigidbody2D>();
            if (origRb != null)
            {
                origRb.position = exitPosition;
                origRb.gravityScale = session.originalGravityScale;
                origRb.linearVelocity = exitVelocity;
                origRb.WakeUp();
            }

            Physics2D.SyncTransforms();
        }

        if (currentFloatingDisabledObject == session.originalObject)
            currentFloatingDisabledObject = null;
        if (activeParticleProxyInstance == session.particleProxyObject)
            activeParticleProxyInstance = null;
    }

    private void RestoreAllFloatingSessions()
    {
        if (m_proxyToSession.Count == 0) return;

        List<GameObject> keys = new List<GameObject>(m_proxyToSession.Keys);
        for (int i = 0; i < keys.Count; i++)
        {
            EndFloatingSessionForProxy(keys[i]);
        }
    }

    private GameObject ResolveFloatingTargetObject(Collider2D col)
    {
        if (col == null) return null;

        GameObject candidate = col.attachedRigidbody != null ? col.attachedRigidbody.gameObject : col.gameObject;
        if (candidate == gameObject || candidate.transform.IsChildOf(transform) || transform.IsChildOf(candidate.transform))
            return null;

        List<string> tagsToCheck = (shadowFloatTags != null && shadowFloatTags.Count > 0) ? shadowFloatTags : interactingTags;
        if (tagsToCheck == null || tagsToCheck.Count == 0)
            return null;

        Transform root = candidate.transform.root;
        for (int i = 0; i < tagsToCheck.Count; i++)
        {
            string allowedTag = tagsToCheck[i];
            if (string.IsNullOrWhiteSpace(allowedTag))
                continue;

            if (candidate.tag == allowedTag)
                return candidate;
            if (col.gameObject.tag == allowedTag)
                return col.gameObject;
            if (root != null && root.gameObject.tag == allowedTag)
                return root.gameObject;
        }

        return null;
    }

    private Vector2 ReadFloatingSteerInput()
    {
        Vector2 input = Vector2.zero;

        // 1. Custom New Input System InputActionReference (if assigned in Inspector)
        if (shadowFloatMoveActionReference != null && shadowFloatMoveActionReference.action != null)
        {
            if (!shadowFloatMoveActionReference.action.enabled)
                shadowFloatMoveActionReference.action.Enable();

            Vector2 refVal = shadowFloatMoveActionReference.action.ReadValue<Vector2>();
            if (refVal.sqrMagnitude > input.sqrMagnitude)
                input = refVal;
        }

        // 2. Custom inline New Input System InputAction (defaults to <Gamepad>/dpad)
        if (shadowFloatMoveAction != null)
        {
            if (!shadowFloatMoveAction.enabled)
                shadowFloatMoveAction.Enable();

            Vector2 actionVal = shadowFloatMoveAction.ReadValue<Vector2>();
            if (actionVal.sqrMagnitude > input.sqrMagnitude)
                input = actionVal;
        }

        // 3. Direct Keyboard / Gamepad D-Pad & Left Stick fallback
        if (Keyboard.current != null)
        {
            Vector2 kbInput = Vector2.zero;
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) kbInput.x -= 1f;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) kbInput.x += 1f;
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed || Keyboard.current.spaceKey.isPressed) kbInput.y += 1f;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) kbInput.y -= 1f;
            if (kbInput.sqrMagnitude > input.sqrMagnitude) input = kbInput;
        }

        if (Gamepad.current != null)
        {
            Vector2 stick = Gamepad.current.leftStick.ReadValue();
            Vector2 dpad = Gamepad.current.dpad.ReadValue();
            if (stick.sqrMagnitude > input.sqrMagnitude) input = stick;
            if (dpad.sqrMagnitude > input.sqrMagnitude) input = dpad;
        }

        return Vector2.ClampMagnitude(input, 1f);
    }

    private static GameObject CreateFallbackParticleProxy(Vector3 position, string targetName)
    {
        GameObject fallbackObj = new GameObject($"{targetName}_ShadowFloatParticles");
        fallbackObj.transform.position = position;

        ParticleSystem ps = fallbackObj.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true;
        main.startLifetime = 0.6f;
        main.startSpeed = 0.8f;
        main.startSize = 0.18f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.rateOverTime = 28f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.25f;

        return fallbackObj;
    }

    /// <summary>
    /// Projects sprite-local non-transparent points by shadowWidthFactor and currentShadowDir * currentLength
    /// into the target transform's local space.
    /// </summary>
    private void TransformProjectedPoints(Vector2[] spriteLocalPoints, Vector3[] outTargetLocalPoints, Transform targetTransform)
    {
        Transform spriteTransform = spriteRenderer != null ? spriteRenderer.transform : transform;
        bool flipX = spriteRenderer != null && spriteRenderer.flipX;
        bool flipY = spriteRenderer != null && spriteRenderer.flipY;

        Vector2 worldDir = currentShadowDir * currentLength;
        Vector2 shadowAxis = currentShadowDir.sqrMagnitude > 0.0001f ? currentShadowDir.normalized : Vector2.down;

        int count = spriteLocalPoints.Length;
        if (worldScaledBuffer == null || worldScaledBuffer.Length < count)
        {
            worldScaledBuffer = new Vector2[count];
        }

        float minProj = float.MaxValue;
        float maxProj = float.MinValue;

        for (int i = 0; i < count; i++)
        {
            Vector2 pt = spriteLocalPoints[i];
            Vector2 scaled = opaqueCenter + (pt - opaqueCenter) * shadowWidthFactor;
            if (flipX) scaled.x = -scaled.x;
            if (flipY) scaled.y = -scaled.y;

            Vector2 worldPt = spriteTransform.TransformPoint(scaled);
            worldScaledBuffer[i] = worldPt;

            float proj = Vector2.Dot(worldPt, shadowAxis);
            if (proj < minProj) minProj = proj;
            if (proj > maxProj) maxProj = proj;
        }

        float projSpan = Mathf.Max(maxProj - minProj, 0.0001f);

        for (int i = 0; i < count; i++)
        {
            Vector2 worldPt = worldScaledBuffer[i];
            float t = Mathf.Clamp01((Vector2.Dot(worldPt, shadowAxis) - minProj) / projSpan);
            Vector2 projectedWorld = worldPt + worldDir * t;

            Vector3 localPos = targetTransform.InverseTransformPoint(projectedWorld);
            outTargetLocalPoints[i] = new Vector3(localPos.x, localPos.y, 0f);
        }
    }

    private void TransformProjectedWorldPoints(Vector2[] spriteLocalPoints, Vector2[] outWorldPoints)
    {
        Transform spriteTransform = spriteRenderer != null ? spriteRenderer.transform : transform;
        bool flipX = spriteRenderer != null && spriteRenderer.flipX;
        bool flipY = spriteRenderer != null && spriteRenderer.flipY;

        Vector2 worldDir = currentShadowDir * currentLength;
        Vector2 shadowAxis = currentShadowDir.sqrMagnitude > 0.0001f ? currentShadowDir.normalized : Vector2.down;

        int count = spriteLocalPoints.Length;
        float minProj = float.MaxValue;
        float maxProj = float.MinValue;

        for (int i = 0; i < count; i++)
        {
            Vector2 pt = spriteLocalPoints[i];
            Vector2 scaled = opaqueCenter + (pt - opaqueCenter) * shadowWidthFactor;
            if (flipX) scaled.x = -scaled.x;
            if (flipY) scaled.y = -scaled.y;

            Vector2 worldPt = spriteTransform.TransformPoint(scaled);
            outWorldPoints[i] = worldPt;

            float proj = Vector2.Dot(worldPt, shadowAxis);
            if (proj < minProj) minProj = proj;
            if (proj > maxProj) maxProj = proj;
        }

        float projSpan = Mathf.Max(maxProj - minProj, 0.0001f);

        for (int i = 0; i < count; i++)
        {
            Vector2 worldPt = outWorldPoints[i];
            float t = Mathf.Clamp01((Vector2.Dot(worldPt, shadowAxis) - minProj) / projSpan);
            outWorldPoints[i] = worldPt + worldDir * t;
        }
    }

    private float GetOrientationSign(Transform targetTransform)
    {
        Transform spriteTransform = spriteRenderer != null ? spriteRenderer.transform : transform;
        bool flipX = spriteRenderer != null && spriteRenderer.flipX;
        bool flipY = spriteRenderer != null && spriteRenderer.flipY;

        float spriteSign = Mathf.Sign(spriteTransform.lossyScale.x * spriteTransform.lossyScale.y);
        if (flipX) spriteSign = -spriteSign;
        if (flipY) spriteSign = -spriteSign;

        float targetSign = Mathf.Sign(targetTransform.lossyScale.x * targetTransform.lossyScale.y);
        return spriteSign * targetSign;
    }

    private int[] GetOrientedTriangles(Transform targetTransform)
    {
        if (GetOrientationSign(targetTransform) >= 0f)
            return baseOpaqueTriangles;

        if (flippedOpaqueTriangles == null || flippedOpaqueTriangles.Length != baseOpaqueTriangles.Length)
        {
            flippedOpaqueTriangles = new int[baseOpaqueTriangles.Length];
            for (int i = 0; i < baseOpaqueTriangles.Length; i += 3)
            {
                flippedOpaqueTriangles[i] = baseOpaqueTriangles[i];
                flippedOpaqueTriangles[i + 1] = baseOpaqueTriangles[i + 2];
                flippedOpaqueTriangles[i + 2] = baseOpaqueTriangles[i + 1];
            }
        }
        return flippedOpaqueTriangles;
    }

    /// <summary>
    /// Reads the sprite texture's alpha channel and builds a mesh + collider contours
    /// strictly from non-transparent pixels (alpha > alphaThreshold).
    /// </summary>
    private void EnsureOpaqueGeometryCached()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInParent<SpriteRenderer>();

        if (spriteRenderer == null || spriteRenderer.sprite == null)
        {
            baseOpaqueVertices = null;
            baseOpaqueTriangles = null;
            flippedOpaqueTriangles = null;
            baseOpaqueContours.Clear();
            return;
        }

        Sprite sprite = spriteRenderer.sprite;
        int clampedRes = Mathf.Clamp(maxPixelSampleResolution, 16, 256);

        if (cachedSprite == sprite &&
            Mathf.Approximately(cachedAlphaThreshold, alphaThreshold) &&
            cachedSampleResolution == clampedRes &&
            baseOpaqueVertices != null)
        {
            return;
        }

        cachedSprite = sprite;
        cachedAlphaThreshold = alphaThreshold;
        cachedSampleResolution = clampedRes;
        flippedOpaqueTriangles = null;
        geometryDirty = true;

        BuildOpaquePixelGeometry(sprite, alphaThreshold, clampedRes);
    }

    private void BuildOpaquePixelGeometry(Sprite sprite, float threshold, int maxRes)
    {
        baseOpaqueContours.Clear();

        Rect texRect = sprite.textureRect;
        int rectX = Mathf.RoundToInt(texRect.x);
        int rectY = Mathf.RoundToInt(texRect.y);
        int rectW = Mathf.RoundToInt(texRect.width);
        int rectH = Mathf.RoundToInt(texRect.height);

        if (rectW <= 0 || rectH <= 0 || sprite.texture == null)
        {
            baseOpaqueVertices = null;
            baseOpaqueTriangles = null;
            return;
        }

        Color32[] pixels = ReadSpritePixels(sprite.texture, rectX, rectY, rectW, rectH);
        if (pixels == null || pixels.Length != rectW * rectH)
        {
            baseOpaqueVertices = null;
            baseOpaqueTriangles = null;
            return;
        }

        // Determine sampling grid dimensions (1:1 for small sprites, downsampled for large HD textures)
        float scale = Mathf.Min(1f, (float)maxRes / Mathf.Max(rectW, rectH));
        int gridW = Mathf.Max(1, Mathf.RoundToInt(rectW * scale));
        int gridH = Mathf.Max(1, Mathf.RoundToInt(rectH * scale));

        bool[,] opaqueGrid = new bool[gridW, gridH];
        byte alphaCutoff = (byte)Mathf.Clamp(Mathf.RoundToInt(threshold * 255f), 1, 254);

        Vector2 pivot = sprite.pivot;
        float ppu = sprite.pixelsPerUnit;
        float cellPixelW = (float)rectW / gridW;
        float cellPixelH = (float)rectH / gridH;

        Vector2 centerAccum = Vector2.zero;
        int opaqueCellCount = 0;

        for (int gy = 0; gy < gridH; gy++)
        {
            int pyStart = Mathf.FloorToInt(gy * cellPixelH);
            int pyEnd = Mathf.Min(rectH, Mathf.CeilToInt((gy + 1) * cellPixelH));

            for (int gx = 0; gx < gridW; gx++)
            {
                int pxStart = Mathf.FloorToInt(gx * cellPixelW);
                int pxEnd = Mathf.Min(rectW, Mathf.CeilToInt((gx + 1) * cellPixelW));

                bool isCellOpaque = false;
                for (int py = pyStart; py < pyEnd && !isCellOpaque; py++)
                {
                    int rowOffset = py * rectW;
                    for (int px = pxStart; px < pxEnd; px++)
                    {
                        if (pixels[rowOffset + px].a > alphaCutoff)
                        {
                            isCellOpaque = true;
                            break;
                        }
                    }
                }

                opaqueGrid[gx, gy] = isCellOpaque;
                if (isCellOpaque)
                {
                    float localX = ((gx + 0.5f) * cellPixelW - pivot.x) / ppu;
                    float localY = ((gy + 0.5f) * cellPixelH - pivot.y) / ppu;
                    centerAccum += new Vector2(localX, localY);
                    opaqueCellCount++;
                }
            }
        }

        if (opaqueCellCount == 0)
        {
            baseOpaqueVertices = null;
            baseOpaqueTriangles = null;
            return;
        }

        opaqueCenter = centerAccum / opaqueCellCount;

        // 1. Greedy Meshing of Non-Transparent Cells for the Shadow Mesh
        bool[,] visited = new bool[gridW, gridH];
        List<Vector2> verts = new List<Vector2>();
        List<int> tris = new List<int>();
        Dictionary<long, int> vertexIndexMap = new Dictionary<long, int>();

        int GetOrAddGridVertex(int vx, int vy)
        {
            long key = ((long)vx << 32) | (uint)vy;
            if (vertexIndexMap.TryGetValue(key, out int existingIndex))
                return existingIndex;

            float localX = (vx * cellPixelW - pivot.x) / ppu;
            float localY = (vy * cellPixelH - pivot.y) / ppu;
            int newIndex = verts.Count;
            verts.Add(new Vector2(localX, localY));
            vertexIndexMap[key] = newIndex;
            return newIndex;
        }

        for (int gy = 0; gy < gridH; gy++)
        {
            for (int gx = 0; gx < gridW; gx++)
            {
                if (!opaqueGrid[gx, gy] || visited[gx, gy])
                    continue;

                // Expand width along X
                int quadW = 1;
                while (gx + quadW < gridW && opaqueGrid[gx + quadW, gy] && !visited[gx + quadW, gy])
                {
                    quadW++;
                }

                // Expand height along Y while the full row span is opaque and unvisited
                int quadH = 1;
                while (gy + quadH < gridH)
                {
                    bool rowValid = true;
                    for (int k = 0; k < quadW; k++)
                    {
                        if (!opaqueGrid[gx + k, gy + quadH] || visited[gx + k, gy + quadH])
                        {
                            rowValid = false;
                            break;
                        }
                    }
                    if (!rowValid) break;
                    quadH++;
                }

                // Mark cells as visited
                for (int dy = 0; dy < quadH; dy++)
                {
                    for (int dx = 0; dx < quadW; dx++)
                    {
                        visited[gx + dx, gy + dy] = true;
                    }
                }

                // Emit quad (counter-clockwise in 2D -> clockwise for Unity -Z camera normal)
                int v0 = GetOrAddGridVertex(gx, gy);
                int v1 = GetOrAddGridVertex(gx, gy + quadH);
                int v2 = GetOrAddGridVertex(gx + quadW, gy + quadH);
                int v3 = GetOrAddGridVertex(gx + quadW, gy);

                tris.Add(v0);
                tris.Add(v1);
                tris.Add(v2);

                tris.Add(v0);
                tris.Add(v2);
                tris.Add(v3);
            }
        }

        baseOpaqueVertices = verts.ToArray();
        baseOpaqueTriangles = tris.ToArray();

        // 2. Trace Closed Boundary Contours (Outer Islands + Inner Holes) for PolygonCollider2D
        TraceOpaqueContours(opaqueGrid, gridW, gridH, cellPixelW, cellPixelH, pivot, ppu);
    }

    /// <summary>
    /// Safely reads pixels from a sprite's textureRect even when TextureImporter 'Read/Write Enabled' is false.
    /// </summary>
    private static Color32[] ReadSpritePixels(Texture2D sourceTexture, int x, int y, int width, int height)
    {
        if (sourceTexture.isReadable)
        {
            Color[] colors = sourceTexture.GetPixels(x, y, width, height);
            Color32[] result = new Color32[colors.Length];
            for (int i = 0; i < colors.Length; i++)
                result[i] = colors[i];
            return result;
        }

        RenderTexture rt = RenderTexture.GetTemporary(
            sourceTexture.width,
            sourceTexture.height,
            0,
            RenderTextureFormat.Default,
            RenderTextureReadWrite.Linear);

        Graphics.Blit(sourceTexture, rt);
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = rt;

        Texture2D tempTex = new Texture2D(sourceTexture.width, sourceTexture.height, TextureFormat.RGBA32, false);
        tempTex.ReadPixels(new Rect(0, 0, sourceTexture.width, sourceTexture.height), 0, 0);
        tempTex.Apply();

        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(rt);

        Color[] subColors = tempTex.GetPixels(x, y, width, height);
        Destroy(tempTex);

        Color32[] subResult = new Color32[subColors.Length];
        for (int i = 0; i < subColors.Length; i++)
            subResult[i] = subColors[i];

        return subResult;
    }

    private struct GridEdge : IEquatable<GridEdge>
    {
        public Vector2Int start;
        public Vector2Int end;

        public GridEdge(Vector2Int s, Vector2Int e)
        {
            start = s;
            end = e;
        }

        public bool Equals(GridEdge other) => start == other.start && end == other.end;
        public override bool Equals(object obj) => obj is GridEdge other && Equals(other);
        public override int GetHashCode() => (start.GetHashCode() * 397) ^ end.GetHashCode();
    }

    /// <summary>
    /// Extracts simplified polygon loops around all non-transparent pixel boundaries (including interior holes).
    /// </summary>
    private void TraceOpaqueContours(bool[,] opaqueGrid, int gridW, int gridH, float cellPixelW, float cellPixelH, Vector2 pivot, float ppu)
    {
        Dictionary<Vector2Int, List<Vector2Int>> outgoingByVertex = new Dictionary<Vector2Int, List<Vector2Int>>();
        List<GridEdge> allEdges = new List<GridEdge>();

        void AddDirectedEdge(Vector2Int a, Vector2Int b)
        {
            if (!outgoingByVertex.TryGetValue(a, out List<Vector2Int> list))
            {
                list = new List<Vector2Int>(2);
                outgoingByVertex[a] = list;
            }
            list.Add(b);
            allEdges.Add(new GridEdge(a, b));
        }

        bool IsOpaque(int x, int y) => x >= 0 && x < gridW && y >= 0 && y < gridH && opaqueGrid[x, y];

        for (int y = 0; y < gridH; y++)
        {
            for (int x = 0; x < gridW; x++)
            {
                if (!opaqueGrid[x, y]) continue;

                // Bottom edge
                if (!IsOpaque(x, y - 1))
                    AddDirectedEdge(new Vector2Int(x, y), new Vector2Int(x + 1, y));
                // Right edge
                if (!IsOpaque(x + 1, y))
                    AddDirectedEdge(new Vector2Int(x + 1, y), new Vector2Int(x + 1, y + 1));
                // Top edge
                if (!IsOpaque(x, y + 1))
                    AddDirectedEdge(new Vector2Int(x + 1, y + 1), new Vector2Int(x, y + 1));
                // Left edge
                if (!IsOpaque(x - 1, y))
                    AddDirectedEdge(new Vector2Int(x, y + 1), new Vector2Int(x, y));
            }
        }

        HashSet<GridEdge> usedEdges = new HashSet<GridEdge>();

        for (int i = 0; i < allEdges.Count; i++)
        {
            GridEdge startEdge = allEdges[i];
            if (usedEdges.Contains(startEdge)) continue;

            List<Vector2Int> loopGridPts = new List<Vector2Int>();
            GridEdge currentEdge = startEdge;

            while (true)
            {
                usedEdges.Add(currentEdge);
                loopGridPts.Add(currentEdge.start);

                Vector2Int currentVertex = currentEdge.end;
                if (currentVertex == startEdge.start)
                    break;

                if (!outgoingByVertex.TryGetValue(currentVertex, out List<Vector2Int> candidates))
                    break;

                Vector2Int prevDir = currentEdge.end - currentEdge.start;
                Vector2Int chosenNext = candidates[0];
                bool foundUnused = false;

                // Prefer right-turn / straight continuity when two contours touch at a corner
                int bestPriority = int.MinValue;
                for (int c = 0; c < candidates.Count; c++)
                {
                    Vector2Int nextV = candidates[c];
                    GridEdge candidateEdge = new GridEdge(currentVertex, nextV);
                    if (usedEdges.Contains(candidateEdge)) continue;

                    Vector2Int nextDir = nextV - currentVertex;
                    int cross = prevDir.x * nextDir.y - prevDir.y * nextDir.x;
                    int dot = prevDir.x * nextDir.x + prevDir.y * nextDir.y;
                    int priority = cross < 0 ? 3 : (dot > 0 ? 2 : (cross > 0 ? 1 : 0));

                    if (!foundUnused || priority > bestPriority)
                    {
                        foundUnused = true;
                        bestPriority = priority;
                        chosenNext = nextV;
                    }
                }

                if (!foundUnused) break;
                currentEdge = new GridEdge(currentVertex, chosenNext);
            }

            if (loopGridPts.Count < 3) continue;

            // Remove collinear intermediate grid vertices so straight edges have only 2 endpoints
            List<Vector2> simplified = new List<Vector2>();
            int count = loopGridPts.Count;
            for (int k = 0; k < count; k++)
            {
                Vector2Int prev = loopGridPts[(k - 1 + count) % count];
                Vector2Int curr = loopGridPts[k];
                Vector2Int next = loopGridPts[(k + 1) % count];

                Vector2Int d1 = curr - prev;
                Vector2Int d2 = next - curr;
                if (d1.x * d2.y - d1.y * d2.x != 0)
                {
                    float localX = (curr.x * cellPixelW - pivot.x) / ppu;
                    float localY = (curr.y * cellPixelH - pivot.y) / ppu;
                    simplified.Add(new Vector2(localX, localY));
                }
            }

            if (simplified.Count >= 3)
            {
                baseOpaqueContours.Add(simplified.ToArray());
            }
        }
    }

    private void OnDrawGizmos()
    {
        if (!showGizmos || drawGizmosOnlyWhenSelected)
            return;

        DrawDetectionRadiusGizmos();
    }

    private void OnDrawGizmosSelected()
    {
        if (!showGizmos || !drawGizmosOnlyWhenSelected)
            return;

        DrawDetectionRadiusGizmos();
    }

    private void DrawDetectionRadiusGizmos()
    {
        Vector2 center = transform.position;
        float radius = Mathf.Max(0f, maxInteractionDistance);

        Transform activeInfluencer = Application.isPlaying
            ? currentInfluencingTransform
            : (affectedByPlayerPosition ? ResolveSingleInfluencingSpriteObject(out _) : null);

        bool hasActiveInfluencer = activeInfluencer != null;

        // 1. Draw Detection Radius 2D Circle + Wire Sphere + Center Crosshair
        Gizmos.color = hasActiveInfluencer ? activeDetectionRadiusColor : idleDetectionRadiusColor;
        DrawWireCircle2D(center, radius, Mathf.Max(16, gizmoCircleSegments));
        Gizmos.DrawWireSphere(center, radius);

        float crossSize = Mathf.Min(0.15f, radius * 0.1f);
        Gizmos.DrawLine(new Vector3(center.x - crossSize, center.y, 0f), new Vector3(center.x + crossSize, center.y, 0f));
        Gizmos.DrawLine(new Vector3(center.x, center.y - crossSize, 0f), new Vector3(center.x, center.y + crossSize, 0f));

        // 2. Highlight the single active influencing Sprite object & draw interaction link
        if (hasActiveInfluencer)
        {
            Vector3 influencerPos = activeInfluencer.position;
            Gizmos.color = influencerLinkColor;
            Gizmos.DrawLine(center, influencerPos);
            DrawWireCircle2D(influencerPos, 0.2f, 24);
            Gizmos.DrawWireSphere(influencerPos, 0.12f);
        }

        // 3. Draw Outward Exit Force Direction Arrow (from ShadowCaster Object -> Player) for BlackEternal shadows
        if (currentShadowType == ShadowType.BlackEternal && applyExitForce && !Mathf.Approximately(exitForceMagnitude, 0f))
        {
            Vector3 playerPos = activeParticleProxyInstance != null
                ? activeParticleProxyInstance.transform.position
                : (hasActiveInfluencer ? activeInfluencer.position : (Vector3)center + transform.TransformDirection(localExitForceDirection.sqrMagnitude > 0.0001f ? localExitForceDirection : Vector2.up).normalized * 1.25f);

            Vector2 fromShadowToPlayer = (Vector2)playerPos - (Vector2)transform.position;
            if (fromShadowToPlayer.sqrMagnitude > 0.0001f)
            {
                Vector2 outwardDir = fromShadowToPlayer.normalized;
                Vector2 arrowDir = exitForceMagnitude >= 0f ? outwardDir : -outwardDir;
                float arrowLength = Mathf.Clamp(Mathf.Abs(exitForceMagnitude) * 0.18f, 0.6f, 2.5f);
                Vector3 tip = playerPos + (Vector3)(arrowDir * arrowLength);

                Gizmos.color = new Color(1f, 0.45f, 0.2f, 0.95f);
                Gizmos.DrawLine(playerPos, tip);

                Vector2 perp = new Vector2(-arrowDir.y, arrowDir.x);
                const float headLen = 0.25f;
                Gizmos.DrawLine(tip, tip - (Vector3)(arrowDir * headLen) + (Vector3)(perp * headLen * 0.55f));
                Gizmos.DrawLine(tip, tip - (Vector3)(arrowDir * headLen) - (Vector3)(perp * headLen * 0.55f));
            }
        }
    }

    private static void DrawWireCircle2D(Vector2 center, float radius, int segments)
    {
        if (radius <= 0f || segments < 3) return;

        float angleStep = (Mathf.PI * 2f) / segments;
        Vector3 prevPoint = new Vector3(center.x + radius, center.y, 0f);

        for (int i = 1; i <= segments; i++)
        {
            float angle = i * angleStep;
            Vector3 nextPoint = new Vector3(
                center.x + Mathf.Cos(angle) * radius,
                center.y + Mathf.Sin(angle) * radius,
                0f
            );
            Gizmos.DrawLine(prevPoint, nextPoint);
            prevPoint = nextPoint;
        }
    }
}

public class ShadowFloatTriggerForwarder2D : MonoBehaviour
{
    private SpriteShadowCaster2D ownerCaster;
    private PolygonCollider2D triggerCollider;

    public void Initialize(SpriteShadowCaster2D caster, PolygonCollider2D collider)
    {
        ownerCaster = caster;
        triggerCollider = collider;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (ownerCaster != null)
            ownerCaster.HandleShadowTriggerEnter(other, triggerCollider);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (ownerCaster != null)
            ownerCaster.HandleShadowTriggerStay(other);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (ownerCaster != null)
            ownerCaster.HandleShadowTriggerExit(other, triggerCollider);
    }
}
