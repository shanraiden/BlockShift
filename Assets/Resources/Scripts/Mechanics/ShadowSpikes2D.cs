using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 2D Spikes hazard designed for BlockShift's Shadow & Pickup mechanics:
/// - Solid White Shadows (WhiteSolidIce): Freeze/cap the spikes into a safe solid walkable bridge.
/// - Eternal Black Shadows (BlackEternal): Phase through harmlessly (or dissolve spikes in darkness).
/// - PickUp Objects (WolfObjectPickup2D / Blocks): Weigh down / jam spikes or act as a shield bridge.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class ShadowSpikes2D : MonoBehaviour
{
    public enum SpikeBehaviorMode
    {
        AlwaysActive,
        TimedCycle,
        ProximityPopUp
    }

    public enum SpikeState
    {
        ActiveLethal,
        RetractedIdle,
        FrozenByWhiteShadow,
        PhasedByBlackShadow,
        WeightedByPickupObject
    }

    [Header("Damage & Knockback")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private float damage = 1f;
    [SerializeField] private float knockbackForce = 9f;
    [SerializeField] private float damageCooldown = 0.5f;
    [SerializeField] private bool knockbackAlongSpikeUp = true;

    [Header("Behavior Mode & Timing")]
    [SerializeField] private SpikeBehaviorMode behaviorMode = SpikeBehaviorMode.AlwaysActive;
    [SerializeField] private float activeCycleDuration = 2.0f;
    [SerializeField] private float retractedCycleDuration = 1.5f;
    [SerializeField] private float proximityTriggerRadius = 3.5f;
    [SerializeField] private float proximityExtendDelay = 0.25f;

    [Header("Solid White Shadow Counterplay (WhiteSolidIce)")]
    [Tooltip("When a WhiteSolidIce shadow (or baked WhiteSolidIce shadow) overlaps these spikes, it freezes & caps them safely.")]
    [SerializeField] private bool whiteShadowNeutralizesSpikes = true;
    [Tooltip("When frozen by WhiteSolidIce, the spikes become a solid walkable surface (isTrigger = false) instead of hurting.")]
    [SerializeField] private bool walkablePlatformWhenWhiteFrozen = true;
    [SerializeField] private Color whiteFrozenTint = new Color(0.82f, 0.95f, 1f, 1f);

    [Header("Eternal Black Shadow Counterplay (BlackEternal)")]
    [Tooltip("Players floating inside a BlackEternal shadow (particle proxy or overlapping BlackEternal trigger) pass through safely.")]
    [SerializeField] private bool blackShadowGrantsPhaseImmunity = true;
    [Tooltip("If true, an overlapping BlackEternal shadow retracts/dissolves the spikes completely while covered.")]
    [SerializeField] private bool blackShadowDissolvesSpikes = true;
    [SerializeField] private Color blackDissolvedTint = new Color(0.25f, 0.25f, 0.35f, 0.45f);

    [Header("PickUp Object Counterplay")]
    [Tooltip("Allows PickUp / dynamic objects dropped or thrown onto the spikes to weigh them down and neutralize them.")]
    [SerializeField] private bool pickupObjectsSuppressSpikes = true;
    [Tooltip("Layers considered PickUp / Heavy objects capable of weighing down spikes (defaults to everything except Player & IgnoreRaycast if unset).")]
    [SerializeField] private LayerMask pickupObjectLayers = ~0;
    [Tooltip("Optional tag filter for PickUp objects (leave empty to allow any non-player Rigidbody2D/Collider2D object).")]
    [SerializeField] private string pickupObjectTag = "";
    [Tooltip("Minimum mass required on a resting object to weigh down the spikes (0 = any object).")]
    [SerializeField] private float minSuppressionMass = 0f;
    [Tooltip("Extra time spikes remain jammed after a PickUp object impacts or leaves them.")]
    [SerializeField] private float suppressionHoldGraceTime = 0.25f;
    [Tooltip("If true, the spikes become a solid support surface when weighed down by a PickUp object so the block rests on top.")]
    [SerializeField] private bool solidPlatformWhenWeighted = true;
    [SerializeField] private Color weightedTint = new Color(0.75f, 0.75f, 0.5f, 1f);

    [Header("Multi-Spike Strip & Retract Animation")]
    [Range(1, 20)]
    [SerializeField] private int spikeCount = 1;
    [Min(0.2f)]
    [SerializeField] private float spikeSpacing = 0.65f;
    [SerializeField] private float retractDistanceY = 0.35f;
    [SerializeField] [Range(0.05f, 1f)] private float retractedScaleY = 0.2f;
    [SerializeField] private float morphTransitionSpeed = 14f;
    [SerializeField] private Color activeSpikeTint = new Color(0.95f, 0.25f, 0.3f, 1f);

    [Header("Events")]
    public UnityEvent<GameObject> onPlayerDamaged;
    public UnityEvent onNeutralizedByWhiteShadow;
    public UnityEvent onDissolvedByBlackShadow;
    public UnityEvent onWeightedByPickupObject;
    public UnityEvent onSpikesRestoredActive;

    [Header("Gizmos")]
    [SerializeField] private bool showGizmos = true;

    [Header("Status (Read-Only)")]
    [SerializeField] private SpikeState currentState = SpikeState.ActiveLethal;

    private Collider2D spikeCollider;
    private Transform visualsRoot;
    private readonly List<SpriteRenderer> m_spikeRenderers = new List<SpriteRenderer>();
    private readonly List<Collider2D> m_overlappingShadowsBuffer = new List<Collider2D>();
    private readonly HashSet<Collider2D> m_suppressingObjects = new HashSet<Collider2D>();

    private float m_nextDamageAllowedTime;
    private float m_cycleTimer;
    private bool m_cycleActive = true;
    private float m_proximityTimer;
    private float m_suppressedUntilTime;
    private float m_currentExtend01 = 1f;
    private Vector3 m_visualsInitialLocalPos;
    private Vector3 m_visualsInitialScale = Vector3.one;
    private SpikeState m_previousState = SpikeState.ActiveLethal;

    private static Sprite s_cachedRuntimeSpikeSprite;

    public SpikeState CurrentState => currentState;
    public bool IsLethal => currentState == SpikeState.ActiveLethal && m_currentExtend01 > 0.35f;

    private void Reset()
    {
        SetupSpikeGameObjects();
    }

    private void Awake()
    {
        spikeCollider = GetComponent<Collider2D>();
        RefreshVisualReferences();

        if (visualsRoot == null || m_spikeRenderers.Count == 0)
        {
            SetupSpikeGameObjects();
        }
        else
        {
            m_visualsInitialLocalPos = visualsRoot.localPosition;
            m_visualsInitialScale = visualsRoot.localScale;
        }

        m_cycleTimer = activeCycleDuration;
        EvaluateSpikeState(0f);
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        EvaluateShadowOverlaps();
        CleanupSuppressingObjects();
        UpdateCycleOrProximity(dt);
        EvaluateSpikeState(dt);
        UpdateSpikeVisualsAndCollider(dt);
    }

    /// <summary>
    /// Builds/rebuilds child Spike visuals and fits the BoxCollider2D to match spikeCount and spikeSpacing.
    /// </summary>
    public void SetupSpikeGameObjects()
    {
        if (spikeCollider == null)
            TryGetComponent(out spikeCollider);

        Transform existingRoot = transform.Find("SpikeVisuals");
        if (existingRoot == null)
        {
            GameObject rootObj = new GameObject("SpikeVisuals");
            rootObj.transform.SetParent(transform, false);
            visualsRoot = rootObj.transform;
        }
        else
        {
            visualsRoot = existingRoot;
        }

        m_visualsInitialLocalPos = visualsRoot.localPosition;
        m_visualsInitialScale = visualsRoot.localScale;

        // Clear and rebuild spike tooth children cleanly
        for (int i = visualsRoot.childCount - 1; i >= 0; i--)
        {
            GameObject child = visualsRoot.GetChild(i).gameObject;
            if (Application.isPlaying)
                Destroy(child);
            else
                DestroyImmediate(child);
        }

        m_spikeRenderers.Clear();
        Sprite spikeSprite = LoadOrCreateSpikeSprite();
        int count = Mathf.Clamp(spikeCount, 1, 20);
        float totalWidth = (count - 1) * spikeSpacing;
        float startX = -totalWidth * 0.5f;

        for (int i = 0; i < count; i++)
        {
            GameObject tooth = new GameObject($"Spike_{i + 1}");
            tooth.transform.SetParent(visualsRoot, false);
            tooth.transform.localPosition = new Vector3(startX + i * spikeSpacing, 0f, 0f);

            SpriteRenderer sr = tooth.AddComponent<SpriteRenderer>();
            sr.sprite = spikeSprite;
            sr.color = activeSpikeTint;
            m_spikeRenderers.Add(sr);
        }

        if (spikeCollider is BoxCollider2D box)
        {
            float colWidth = Mathf.Max(0.55f, totalWidth + spikeSpacing * 0.85f);
            box.size = new Vector2(colWidth, 0.55f);
            box.offset = new Vector2(0f, -0.05f);
            box.isTrigger = true;
        }
    }

    public void RefreshVisualReferences()
    {
        visualsRoot = transform.Find("SpikeVisuals");
        m_spikeRenderers.Clear();
        if (visualsRoot != null)
        {
            visualsRoot.GetComponentsInChildren(true, m_spikeRenderers);
        }
        else
        {
            GetComponentsInChildren(true, m_spikeRenderers);
        }
    }

    private Sprite LoadOrCreateSpikeSprite()
    {
        Sprite loaded = Resources.Load<Sprite>("Sprites/Mechanics/Spike");
        if (loaded != null) return loaded;

        if (s_cachedRuntimeSpikeSprite != null)
            return s_cachedRuntimeSpikeSprite;

        Texture2D tex = BuildSpikeTexture(64);
        s_cachedRuntimeSpikeSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.25f), 64f);
        s_cachedRuntimeSpikeSprite.name = "ShadowSpike_RuntimeSprite";
        return s_cachedRuntimeSpikeSprite;
    }

    public static Texture2D BuildSpikeTexture(int size = 64)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        Color clear = new Color(0f, 0f, 0f, 0f);

        for (int y = 0; y < size; y++)
        {
            float ny = (float)y / (size - 1);
            float halfSpan = (1f - ny) * (size * 0.44f);
            float center = (size - 1) * 0.5f;

            for (int x = 0; x < size; x++)
            {
                float distFromCenter = Mathf.Abs(x - center);
                bool inside = y >= 2 && distFromCenter <= halfSpan;
                if (!inside)
                {
                    tex.SetPixel(x, y, clear);
                }
                else
                {
                    // Subtle metallic bevel shading between left and right faces
                    bool leftFace = x < center;
                    float edgeFactor = distFromCenter / Mathf.Max(1f, halfSpan);
                    float shade = leftFace ? Mathf.Lerp(1f, 0.85f, edgeFactor) : Mathf.Lerp(0.8f, 0.55f, edgeFactor);
                    tex.SetPixel(x, y, new Color(shade, shade, shade, 1f));
                }
            }
        }

        tex.Apply();
        return tex;
    }

    #region Shadow & Pickup Evaluation

    private bool m_hasWhiteShadowOverlap;
    private bool m_hasBlackShadowOverlap;

    private void EvaluateShadowOverlaps()
    {
        m_hasWhiteShadowOverlap = false;
        m_hasBlackShadowOverlap = false;

        if (spikeCollider == null)
            return;

        Bounds b = spikeCollider.bounds;
        Vector2 center = b.center;
        Vector2 size = new Vector2(Mathf.Max(0.3f, b.size.x), Mathf.Max(0.3f, b.size.y));
        Collider2D[] hits = Physics2D.OverlapBoxAll(center, size, transform.eulerAngles.z);

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D col = hits[i];
            if (col == null || col == spikeCollider || col.transform.IsChildOf(transform))
                continue;

            // Check live SpriteShadowCaster2D
            SpriteShadowCaster2D caster = col.GetComponent<SpriteShadowCaster2D>();
            if (caster != null)
            {
                if (caster.currentShadowType == SpriteShadowCaster2D.ShadowType.WhiteSolidIce)
                    m_hasWhiteShadowOverlap = true;
                else if (caster.currentShadowType == SpriteShadowCaster2D.ShadowType.BlackEternal)
                    m_hasBlackShadowOverlap = true;
                continue;
            }

            //// Check baked SolidifiedShadow objects
            //if (col.GetComponent<ShadowFloatTriggerForwarder2D>() != null || col.CompareTag("EtherealZone") || col.name.Contains("BlackEternal"))
            //{
            //    print("BLACK SHADOW");
            //    m_hasBlackShadowOverlap = true;
            //}
            //else if (col.name.Contains("WhiteSolidIce"))
            //{
            //    print("WHITE SHADOW");
            //    m_hasWhiteShadowOverlap = true;
            //}
        }
    }

    private void CleanupSuppressingObjects()
    {
        if (m_suppressingObjects.Count == 0 || spikeCollider == null)
            return;

        m_overlappingShadowsBuffer.Clear();
        Bounds b = spikeCollider.bounds;
        b.Expand(new Vector3(0.08f, 0.14f, 0f));

        foreach (Collider2D col in m_suppressingObjects)
        {
            if (col == null || !col.enabled || !col.gameObject.activeInHierarchy || !b.Intersects(col.bounds))
            {
                m_overlappingShadowsBuffer.Add(col);
            }
        }

        for (int i = 0; i < m_overlappingShadowsBuffer.Count; i++)
        {
            m_suppressingObjects.Remove(m_overlappingShadowsBuffer[i]);
        }
    }

    private void UpdateCycleOrProximity(float dt)
    {
        if (behaviorMode == SpikeBehaviorMode.TimedCycle)
        {
            m_cycleTimer -= dt;
            if (m_cycleTimer <= 0f)
            {
                m_cycleActive = !m_cycleActive;
                m_cycleTimer = m_cycleActive ? Mathf.Max(0.2f, activeCycleDuration) : Mathf.Max(0.2f, retractedCycleDuration);
            }
        }
        else if (behaviorMode == SpikeBehaviorMode.ProximityPopUp)
        {
            bool playerNear = IsPlayerWithinRadius(proximityTriggerRadius);
            if (playerNear)
            {
                m_proximityTimer += dt;
                m_cycleActive = m_proximityTimer >= proximityExtendDelay;
            }
            else
            {
                m_proximityTimer = 0f;
                m_cycleActive = false;
            }
        }
        else
        {
            m_cycleActive = true;
        }
    }

    private void EvaluateSpikeState(float dt)
    {
        bool weighted = pickupObjectsSuppressSpikes && (m_suppressingObjects.Count > 0 || Time.time < m_suppressedUntilTime);

        if (whiteShadowNeutralizesSpikes && m_hasWhiteShadowOverlap)
        {
            currentState = SpikeState.FrozenByWhiteShadow;
        }
        else if (blackShadowDissolvesSpikes && m_hasBlackShadowOverlap)
        {
            currentState = SpikeState.PhasedByBlackShadow;
        }
        else if (weighted)
        {
            currentState = SpikeState.WeightedByPickupObject;
        }
        else if (!m_cycleActive)
        {
            currentState = SpikeState.RetractedIdle;
        }
        else
        {
            currentState = SpikeState.ActiveLethal;
        }

        if (currentState != m_previousState)
        {
            m_previousState = currentState;
            switch (currentState)
            {
                case SpikeState.FrozenByWhiteShadow:
                    onNeutralizedByWhiteShadow?.Invoke();
                    break;
                case SpikeState.PhasedByBlackShadow:
                    onDissolvedByBlackShadow?.Invoke();
                    break;
                case SpikeState.WeightedByPickupObject:
                    onWeightedByPickupObject?.Invoke();
                    break;
                case SpikeState.ActiveLethal:
                    onSpikesRestoredActive?.Invoke();
                    break;
            }
        }
    }

    private void UpdateSpikeVisualsAndCollider(float dt)
    {
        float targetExtend = currentState == SpikeState.ActiveLethal ? 1f :
                             currentState == SpikeState.FrozenByWhiteShadow ? 0.45f : 0f;

        if (dt > 0f)
        {
            m_currentExtend01 = Mathf.Lerp(m_currentExtend01, targetExtend, 1f - Mathf.Exp(-Mathf.Max(1f, morphTransitionSpeed) * dt));
        }
        else
        {
            m_currentExtend01 = targetExtend;
        }

        if (visualsRoot != null)
        {
            float yOffset = -retractDistanceY * (1f - m_currentExtend01);
            float yScale = Mathf.Lerp(retractedScaleY, 1f, m_currentExtend01);
            visualsRoot.localPosition = m_visualsInitialLocalPos + new Vector3(0f, yOffset, 0f);
            visualsRoot.localScale = new Vector3(m_visualsInitialScale.x, m_visualsInitialScale.y * yScale, m_visualsInitialScale.z);
        }

        Color targetColor = activeSpikeTint;
        switch (currentState)
        {
            case SpikeState.FrozenByWhiteShadow:
                targetColor = whiteFrozenTint;
                break;
            case SpikeState.PhasedByBlackShadow:
                targetColor = blackDissolvedTint;
                break;
            case SpikeState.WeightedByPickupObject:
                targetColor = weightedTint;
                break;
        }

        for (int i = 0; i < m_spikeRenderers.Count; i++)
        {
            if (m_spikeRenderers[i] != null)
            {
                m_spikeRenderers[i].color = Color.Lerp(m_spikeRenderers[i].color, targetColor, dt > 0f ? Mathf.Clamp01(dt * morphTransitionSpeed) : 1f);
            }
        }

        // Solidify spikes into a walkable/supporting platform when frozen by WhiteSolidIce or weighed down by a PickUp object
        if (spikeCollider != null)
        {
            bool shouldBeSolidPlatform =
                (currentState == SpikeState.FrozenByWhiteShadow && walkablePlatformWhenWhiteFrozen) ||
                (currentState == SpikeState.WeightedByPickupObject && solidPlatformWhenWeighted);
            spikeCollider.isTrigger = !shouldBeSolidPlatform;
        }
    }

    #endregion

    #region Triggers & Damage

    private void OnTriggerEnter2D(Collider2D other)
    {
        HandleIncomingCollider(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        HandleIncomingCollider(other);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision != null)
            HandleIncomingCollider(collision.collider);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (collision != null)
            HandleIncomingCollider(collision.collider);
    }

    private void HandleIncomingCollider(Collider2D other)
    {
        if (other == null || other == spikeCollider || other.transform.IsChildOf(transform))
            return;

        // 1. Check if a PickUp / heavy prop landed on or is covering the spikes
        if (pickupObjectsSuppressSpikes && IsValidPickupSuppressor(other))
        {
            m_suppressingObjects.Add(other);
            m_suppressedUntilTime = Time.time + Mathf.Max(0f, suppressionHoldGraceTime);
            return;
        }

        // 2. Check if this is the Player (or floating BlackEternal particle proxy)
        if (!IsPlayerOrShadowProxy(other))
            return;

        // Grant immunity if player is floating inside an Eternal Black shadow
        if (blackShadowGrantsPhaseImmunity && (m_hasBlackShadowOverlap || IsShadowParticleProxy(other.gameObject)))
            return;

        if (!IsLethal || Time.time < m_nextDamageAllowedTime)
            return;

        m_nextDamageAllowedTime = Time.time + Mathf.Max(0.05f, damageCooldown);

        GameObject targetObj = other.attachedRigidbody != null ? other.attachedRigidbody.gameObject : other.gameObject;
        Vector2 knockDir = knockbackAlongSpikeUp
            ? (Vector2)transform.up
            : ((Vector2)targetObj.transform.position - (Vector2)transform.position).normalized;

        if (knockDir.sqrMagnitude <= 0.0001f) knockDir = Vector2.up;

        ApplyDamageAndKnockback(targetObj, damage, knockDir.normalized * knockbackForce);
        onPlayerDamaged?.Invoke(targetObj);
    }

    private bool IsValidPickupSuppressor(Collider2D col)
    {
        if (col.isTrigger)
            return false;

        if (IsPlayerOrShadowProxy(col))
            return false;

        if (col.GetComponent<SpriteShadowCaster2D>() != null)
            return false;

        if ((pickupObjectLayers.value & (1 << col.gameObject.layer)) == 0)
            return false;

        if (!string.IsNullOrWhiteSpace(pickupObjectTag))
        {
            Transform root = col.transform.root;
            if (!col.CompareTag(pickupObjectTag) && (root == null || !root.CompareTag(pickupObjectTag)))
                return false;
        }

        // Ignore objects currently held by WolfObjectPickup2D if its collider is somehow active
        if (col.GetComponentInParent<WolfObjectPickup2D>() != null)
            return false;

        Rigidbody2D rb = col.attachedRigidbody;
        if (rb == null || rb.bodyType == RigidbodyType2D.Static)
            return false;

        if (minSuppressionMass > 0f && rb.mass < minSuppressionMass)
            return false;

        return true;
    }

    private bool IsPlayerOrShadowProxy(Collider2D col)
    {
        if (col == null) return false;

        if (IsShadowParticleProxy(col.gameObject))
            return true;

        if (string.IsNullOrWhiteSpace(playerTag))
            return false;

        if (col.CompareTag(playerTag))
            return true;

        Transform root = col.transform.root;
        return root != null && root.CompareTag(playerTag);
    }

    private static bool IsShadowParticleProxy(GameObject obj)
    {
        return obj != null && obj.name.Contains("ShadowFloatParticles");
    }

    private bool IsPlayerWithinRadius(float radius)
    {
        if (string.IsNullOrWhiteSpace(playerTag))
            return false;

        GameObject[] players;
        try
        {
            players = GameObject.FindGameObjectsWithTag(playerTag);
        }
        catch (UnityException)
        {
            return false;
        }

        float sqrRadius = radius * radius;
        Vector2 myPos = transform.position;
        for (int i = 0; i < players.Length; i++)
        {
            if (players[i] != null && players[i].activeInHierarchy)
            {
                if (((Vector2)players[i].transform.position - myPos).sqrMagnitude <= sqrRadius)
                    return true;
            }
        }
        return false;
    }

    private static void ApplyDamageAndKnockback(GameObject targetObj, float dmg, Vector2 impulse)
    {
        if (targetObj == null) return;

        GameObject rootObj = targetObj.transform.root != null ? targetObj.transform.root.gameObject : targetObj;

        if (dmg > 0f)
        {
            targetObj.SendMessage("TakeDamage", dmg, SendMessageOptions.DontRequireReceiver);
            if (rootObj != targetObj)
            {
                rootObj.SendMessage("TakeDamage", dmg, SendMessageOptions.DontRequireReceiver);
            }
        }

        Rigidbody2D rb = targetObj.GetComponentInParent<Rigidbody2D>();
        if (rb != null && rb.bodyType == RigidbodyType2D.Dynamic && impulse.sqrMagnitude > 0.0001f)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
            rb.AddForce(impulse, ForceMode2D.Impulse);
        }
    }

    #endregion

    #region Gizmos

    private void OnDrawGizmos()
    {
        if (!showGizmos) return;

        if (spikeCollider == null)
            TryGetComponent(out spikeCollider);

        Color gizmoCol = activeSpikeTint;
        switch (currentState)
        {
            case SpikeState.FrozenByWhiteShadow:
                gizmoCol = Color.cyan;
                break;
            case SpikeState.PhasedByBlackShadow:
                gizmoCol = new Color(0.55f, 0.4f, 1f, 0.85f);
                break;
            case SpikeState.WeightedByPickupObject:
                gizmoCol = Color.yellow;
                break;
            case SpikeState.RetractedIdle:
                gizmoCol = Color.gray;
                break;
        }

        Gizmos.color = gizmoCol;
        if (spikeCollider != null)
        {
            Bounds b = spikeCollider.bounds;
            Gizmos.DrawWireCube(b.center, new Vector3(b.size.x, b.size.y, 0.05f));
        }

        // Draw knockback / spike orientation arrow
        Vector3 up = transform.up * 0.65f;
        Gizmos.DrawLine(transform.position, transform.position + up);

        if (behaviorMode == SpikeBehaviorMode.ProximityPopUp)
        {
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, proximityTriggerRadius);
        }
    }

    #endregion
}
