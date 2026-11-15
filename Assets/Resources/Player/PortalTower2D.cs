using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using UnityEngine.VFX;

[RequireComponent(typeof(Collider2D))]
public class PortalTower2D : MonoBehaviour
{
    public enum VfxTransportMode
    {
        [Tooltip("The persistent VFX Graph GameObject stays active all the time, remains stationary when idle, and moves with the Player to the destination Portal during teleportation.")]
        TravelToDestinationPortal,

        [Tooltip("The persistent VFX Graph GameObject stays active and stationary at the portal at all times.")]
        StationaryBurstThenTeleport
    }

    private static readonly List<PortalTower2D> s_activePortals = new List<PortalTower2D>();
    private static readonly Dictionary<GameObject, float> s_objectCooldownUntil = new Dictionary<GameObject, float>();

    [Header("Portal Range & Linking Settings")]
    [Tooltip("Maximum distance to search for the closest destination PortalTower2D.")]
    public float maxPortalLinkRange = 20f;

    [Tooltip("Optional manual destination portal override. If left empty, automatically selects the closest PortalTower2D within Max Portal Link Range.")]
    public PortalTower2D linkedPortalOverride;

    [Tooltip("Optional portal network/channel ID. Only portals with the same ID link together (leave empty to link with all portals).")]
    public string portalNetworkID = "";

    [Tooltip("Transform point where teleported objects and VFX enter/exit this portal (defaults to this Transform).")]
    public Transform portalAnchorPoint;

    [Tooltip("Local offset from Portal Anchor Point where arriving objects appear.")]
    public Vector2 exitPositionOffset = Vector2.zero;

    [Header("Teleportable Object Filtering")]
    [Tooltip("LayerMask of objects allowed to enter the portal.")]
    public LayerMask teleportableLayers = ~0;

    [Tooltip("Only GameObjects with one of these tags (on themselves or their root) can be teleported. Leave empty to allow any object on Teleportable Layers.")]
    public List<string> teleportableTags = new List<string> { "Player" };

    [Header("VFX Graph GameObject & Teleportation Timing")]
    [FormerlySerializedAs("teleportParticlePrefab")]
    [Tooltip("VFX Graph GameObject (containing a VisualEffect component) that stays active at all times, rests at the VFX Start Position when idle, moves with the Player during teleportation, and snaps back when completed.")]
    public GameObject teleportVfxGraphObject;

    [Tooltip("VisualEffectAsset (.vfx) used when Teleport VFX Graph Object is not assigned or has no VisualEffectAsset set.")]
    public VisualEffectAsset teleportVfxAsset;

    [Tooltip("Optional Transform override for the VFX GameObject's starting/resting position (defaults to Portal Anchor Point or this Transform).")]
    public Transform vfxStartPoint;

    [Tooltip("Customizable 2D offset from the VFX Start Point (or Portal Anchor Point) where the VFX GameObject rests when idle and snaps back to after teleportation.")]
    public Vector2 vfxStartPositionOffset = Vector2.zero;

    [FormerlySerializedAs("particleTransportMode")]
    [Tooltip("How the VFX Graph GameObject behaves while transporting the object.")]
    public VfxTransportMode vfxTransportMode = VfxTransportMode.TravelToDestinationPortal;

    [Tooltip("Time (in seconds) the teleportation takes while moving the Player and VFX Graph to the destination portal.")]
    [Min(0.05f)]
    public float teleportDuration = 0.65f;

    [Tooltip("Optional arc height (in world units) for the path when traveling to the destination portal (0 = straight line).")]
    public float travelArcHeight = 0.5f;

    [Header("Exit Physics & Cooldown")]
    [Tooltip("Minimum cooldown (in seconds) after arriving at a destination portal before the object can teleport again.")]
    public float reentryCooldown = 0.2f;

    [Header("Events")]
    public UnityEvent<GameObject, PortalTower2D> onTeleportStarted;
    public UnityEvent<GameObject, PortalTower2D> onTeleportCompleted;

    [Header("Gizmo Visualization Settings")]
    public bool showGizmos = true;
    public bool drawGizmosOnlyWhenSelected = false;
    [Range(16, 96)]
    public int gizmoCircleSegments = 48;
    public Color rangeGizmoColor = new Color(0.35f, 0.75f, 1f, 0.65f);
    public Color activeLinkGizmoColor = new Color(0.2f, 1f, 0.55f, 0.95f);
    public Color noLinkGizmoColor = new Color(1f, 0.35f, 0.3f, 0.75f);
    public Color exitPointGizmoColor = new Color(1f, 0.75f, 0.15f, 0.95f);
    public Color vfxStartPointGizmoColor = new Color(0.15f, 0.85f, 1f, 0.95f);

    [Header("Status (Read-Only)")]
    [SerializeField] private PortalTower2D currentClosestPortal;
    [SerializeField] private GameObject currentlyTransportingObject;
    [SerializeField] private GameObject activeVfxObject;

    private Collider2D portalTriggerCollider;
    private bool m_vfxInitialized;
    private readonly HashSet<GameObject> m_activeTransports = new HashSet<GameObject>();
    private readonly Dictionary<GameObject, float> m_arrivedObjectsWaitingForExit = new Dictionary<GameObject, float>();
    private readonly List<GameObject> m_exitCleanupList = new List<GameObject>();

    public PortalTower2D ClosestLinkedPortal => FindClosestPortalInRange();

    #region Unity Lifecycle

    private void Reset() => EnsureTriggerCollider();

    private void OnValidate()
    {
        EnsureTriggerCollider();

        if (m_activeTransports.Count == 0)
        {
            if (activeVfxObject != null && activeVfxObject != gameObject)
            {
                activeVfxObject.transform.position = GetVfxStartWorldPosition();
            }
            else if (teleportVfxGraphObject != null && teleportVfxGraphObject != gameObject && teleportVfxGraphObject.scene.IsValid())
            {
                teleportVfxGraphObject.transform.position = GetVfxStartWorldPosition();
            }
        }
    }

    private void Awake()
    {
        EnsureTriggerCollider();
        EnsurePersistentVfxActive();
    }

    private void Start()
    {
        EnsurePersistentVfxActive();
    }

    private void OnEnable()
    {
        EnsureTriggerCollider();
        if (!s_activePortals.Contains(this))
            s_activePortals.Add(this);

        EnsurePersistentVfxActive();
    }

    private void OnDisable()
    {
        s_activePortals.Remove(this);
        m_arrivedObjectsWaitingForExit.Clear();
    }

    private void Update()
    {
        currentClosestPortal = FindClosestPortalInRange();

        // Keep the VFX Graph GameObject active at all times and resting at its customizable start position when idle
        if (activeVfxObject != null)
        {
            if (!activeVfxObject.activeSelf)
                activeVfxObject.SetActive(true);

            if (m_activeTransports.Count == 0)
            {
                activeVfxObject.transform.position = GetVfxStartWorldPosition();
            }
        }
    }

    private void FixedUpdate()
    {
        UpdateArrivedObjectsExitState();
        ScanOverlappingColliders();
    }

    private void OnTriggerEnter2D(Collider2D other) => HandleTrigger(other);
    private void OnTriggerStay2D(Collider2D other) => HandleTrigger(other);

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other == null || m_arrivedObjectsWaitingForExit.Count == 0)
            return;

        GameObject exitedObj = ResolveTeleportableObject(other);
        if (exitedObj != null && m_arrivedObjectsWaitingForExit.TryGetValue(exitedObj, out float arrivedTime))
        {
            if (Time.time - arrivedTime >= 0.08f && !IsObjectOverlappingPortal(exitedObj))
            {
                m_arrivedObjectsWaitingForExit.Remove(exitedObj);
            }
        }
    }

    #endregion

    #region Trigger & Teleportation Logic

    private void EnsureTriggerCollider()
    {
        if (portalTriggerCollider == null)
            TryGetComponent(out portalTriggerCollider);

        if (portalTriggerCollider != null && !portalTriggerCollider.isTrigger)
            portalTriggerCollider.isTrigger = true;
    }

    private void ScanOverlappingColliders()
    {
        if (portalTriggerCollider == null || !portalTriggerCollider.enabled)
            return;

        Bounds bounds = portalTriggerCollider.bounds;
        Collider2D[] hits = Physics2D.OverlapBoxAll(bounds.center, bounds.size, transform.eulerAngles.z);
        if (hits == null || hits.Length == 0)
            return;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D col = hits[i];
            if (col == null || col == portalTriggerCollider)
                continue;

            if (portalTriggerCollider.IsTouching(col) || bounds.Intersects(col.bounds))
            {
                HandleTrigger(col);
            }
        }
    }

    private void UpdateArrivedObjectsExitState()
    {
        if (m_arrivedObjectsWaitingForExit.Count == 0)
            return;

        m_exitCleanupList.Clear();
        foreach (var kvp in m_arrivedObjectsWaitingForExit)
        {
            GameObject obj = kvp.Key;
            float arrivedTime = kvp.Value;

            if (obj == null || !obj.activeInHierarchy)
            {
                m_exitCleanupList.Add(obj);
                continue;
            }

            if (Time.time - arrivedTime < 0.08f)
                continue;

            if (!IsObjectOverlappingPortal(obj))
            {
                m_exitCleanupList.Add(obj);
            }
        }

        for (int i = 0; i < m_exitCleanupList.Count; i++)
        {
            m_arrivedObjectsWaitingForExit.Remove(m_exitCleanupList[i]);
        }
    }

    private bool IsObjectOverlappingPortal(GameObject obj)
    {
        if (obj == null || portalTriggerCollider == null || !portalTriggerCollider.enabled)
            return false;

        Bounds portalBounds = portalTriggerCollider.bounds;
        Collider2D[] objColliders = obj.GetComponentsInChildren<Collider2D>();
        for (int i = 0; i < objColliders.Length; i++)
        {
            Collider2D col = objColliders[i];
            if (col == null || !col.enabled)
                continue;

            if (portalTriggerCollider.IsTouching(col) || portalBounds.Intersects(col.bounds))
                return true;
        }

        return false;
    }

    private void HandleTrigger(Collider2D other)
    {
        if (other == null)
            return;

        GameObject targetObj = ResolveTeleportableObject(other);
        if (targetObj != null)
        {
            TryTeleportObject(targetObj);
        }
    }

    /// <summary>
    /// Attempts to teleport the given GameObject to the closest PortalTower2D within maxPortalLinkRange.
    /// </summary>
    public bool TryTeleportObject(GameObject incomingObject)
    {
        if (incomingObject == null || !incomingObject.activeInHierarchy)
            return false;

        if (m_activeTransports.Contains(incomingObject))
            return false;

        // Prevent immediate re-teleportation at the destination portal until the object steps out of it
        if (m_arrivedObjectsWaitingForExit.ContainsKey(incomingObject))
            return false;

        if (s_objectCooldownUntil.TryGetValue(incomingObject, out float cooldownUntil) && Time.time < cooldownUntil)
            return false;

        PortalTower2D destinationPortal = FindClosestPortalInRange();
        if (destinationPortal == null || destinationPortal == this)
            return false;

        StartCoroutine(TeleportRoutine(incomingObject, destinationPortal));
        return true;
    }

    private IEnumerator TeleportRoutine(GameObject transportingObj, PortalTower2D destinationPortal)
    {
        m_activeTransports.Add(transportingObj);
        m_arrivedObjectsWaitingForExit.Remove(transportingObj);
        currentlyTransportingObject = transportingObj;

        Vector3 vfxStartPos = GetVfxStartWorldPosition();
        Vector3 vfxEndPos = destinationPortal.GetVfxStartWorldPosition();
        Vector3 playerExitPos = destinationPortal.GetExitWorldPosition();

        Rigidbody2D rb = transportingObj.GetComponent<Rigidbody2D>();
        ResetRigidbodyVelocity(rb);

        onTeleportStarted?.Invoke(transportingObj, destinationPortal);

        // 1. Ensure the persistent VFX Graph GameObject is active and ready
        GameObject vfxObj = EnsurePersistentVfxActive();
        if (vfxObj == null && destinationPortal != null)
        {
            vfxObj = destinationPortal.EnsurePersistentVfxActive();
        }

        // 2. Disable incoming GameObject while transporting and snap VFX to the customizable starting position
        transportingObj.SetActive(false);
        transportingObj.transform.position = vfxStartPos;

        if (vfxObj != null)
        {
            vfxObj.SetActive(true);
            vfxObj.transform.position = vfxStartPos;
            SetVfxComponentsEnabled(vfxObj, true);
        }

        // 3. Move VFX Graph GameObject from this Portal's VFX Start Position to the Destination Portal's VFX Start Position
        float duration = Mathf.Max(0.05f, teleportDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float smoothT = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));

            vfxStartPos = GetVfxStartWorldPosition();
            if (destinationPortal != null)
            {
                vfxEndPos = destinationPortal.GetVfxStartWorldPosition();
            }

            Vector3 linearPos = Vector3.Lerp(vfxStartPos, vfxEndPos, smoothT);
            float parabolaArc = 4f * smoothT * (1f - smoothT) * travelArcHeight;
            Vector3 currentPos = linearPos + new Vector3(0f, parabolaArc, 0f);

            if (transportingObj != null)
            {
                transportingObj.transform.position = currentPos;
            }

            if (vfxObj != null)
            {
                if (!vfxObj.activeSelf)
                    vfxObj.SetActive(true);

                if (vfxTransportMode == VfxTransportMode.TravelToDestinationPortal)
                {
                    vfxObj.transform.position = currentPos;
                }
            }

            yield return null;
        }

        // 4. Move transporting GameObject to destination portal, reactivate, and zero velocity
        if (transportingObj != null)
        {
            if (destinationPortal != null)
            {
                playerExitPos = destinationPortal.GetExitWorldPosition();
                destinationPortal.m_arrivedObjectsWaitingForExit[transportingObj] = Time.time;
            }

            float cooldownDuration = destinationPortal != null ? destinationPortal.reentryCooldown : reentryCooldown;
            s_objectCooldownUntil[transportingObj] = Time.time + Mathf.Clamp(cooldownDuration, 0.05f, 0.25f);

            transportingObj.transform.position = playerExitPos;
            transportingObj.SetActive(true);
            transportingObj.transform.position = playerExitPos;

            if (rb != null)
            {
                rb.position = playerExitPos;
                ResetRigidbodyVelocity(rb);
                rb.WakeUp();
            }

            Physics2D.SyncTransforms();

            onTeleportCompleted?.Invoke(transportingObj, destinationPortal);
            if (destinationPortal != null)
            {
                destinationPortal.onTeleportCompleted?.Invoke(transportingObj, this);
            }
        }

        // 5. Return VFX GameObject to its Starting Position:
        //    First disable its VisualEffect component(s), snap to Starting Position, then re-enable once reached
        if (vfxObj != null)
        {
            vfxObj.SetActive(true);
            SetVfxComponentsEnabled(vfxObj, false);
            vfxObj.transform.position = GetVfxStartWorldPosition();

            yield return null;

            if (vfxObj != null)
            {
                vfxObj.transform.position = GetVfxStartWorldPosition();
                SetVfxComponentsEnabled(vfxObj, true);
            }
        }

        m_activeTransports.Remove(transportingObj);
        if (currentlyTransportingObject == transportingObj)
            currentlyTransportingObject = null;
    }

    private static void ResetRigidbodyVelocity(Rigidbody2D targetRb)
    {
        if (targetRb == null || targetRb.bodyType == RigidbodyType2D.Static)
            return;

        targetRb.linearVelocity = Vector2.zero;
        targetRb.angularVelocity = 0f;
    }

    #endregion

    #region Object Resolution

    private GameObject ResolveTeleportableObject(Collider2D col)
    {
        if (col == null)
            return null;

        GameObject candidate = col.attachedRigidbody != null ? col.attachedRigidbody.gameObject : col.gameObject;
        if (candidate == gameObject || candidate.transform.IsChildOf(transform) || transform.IsChildOf(candidate.transform))
            return null;

        Transform root = candidate.transform.root;
        GameObject rootObj = root != null ? root.gameObject : candidate;

        // 1. If teleportableTags has entries, check if root, candidate, or collider matches any allowed tag
        if (teleportableTags != null && teleportableTags.Count > 0)
        {
            for (int i = 0; i < teleportableTags.Count; i++)
            {
                string allowedTag = teleportableTags[i];
                if (string.IsNullOrWhiteSpace(allowedTag))
                    continue;

                allowedTag = allowedTag.Trim();

                if (rootObj != null && HasTagSafe(rootObj, allowedTag))
                    return rootObj;
                if (HasTagSafe(candidate, allowedTag))
                    return candidate;
                if (HasTagSafe(col.gameObject, allowedTag))
                    return candidate;
            }

            return null;
        }

        // 2. If teleportableTags is empty, always allow the Player tag or objects on teleportableLayers
        if (rootObj != null && HasTagSafe(rootObj, "Player"))
            return rootObj;
        if (HasTagSafe(candidate, "Player"))
            return candidate;

        bool layerValid = IsInLayerMask(candidate.layer, teleportableLayers) ||
                          IsInLayerMask(col.gameObject.layer, teleportableLayers) ||
                          (rootObj != null && IsInLayerMask(rootObj.layer, teleportableLayers));

        return layerValid ? candidate : null;
    }

    private static bool HasTagSafe(GameObject obj, string tagToMatch)
    {
        if (obj == null || string.IsNullOrEmpty(tagToMatch))
            return false;

        return string.Equals(obj.tag, tagToMatch, System.StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsInLayerMask(int layer, LayerMask mask) => (mask.value & (1 << layer)) != 0;

    #endregion

    #region Portal Linking & Positioning

    public PortalTower2D FindClosestPortalInRange()
    {
        Vector2 myPos = GetAnchorWorldPosition();
        float maxRangeSqr = maxPortalLinkRange * maxPortalLinkRange;

        if (IsValidDestinationPortal(linkedPortalOverride))
        {
            float overrideSqrDist = ((Vector2)linkedPortalOverride.GetAnchorWorldPosition() - myPos).sqrMagnitude;
            if (overrideSqrDist <= maxRangeSqr)
                return linkedPortalOverride;
        }

        IReadOnlyList<PortalTower2D> portals = Application.isPlaying
            ? (IReadOnlyList<PortalTower2D>)s_activePortals
            : FindObjectsByType<PortalTower2D>(FindObjectsSortMode.None);

        PortalTower2D closest = null;
        float closestSqrDist = maxRangeSqr;

        for (int i = 0; i < portals.Count; i++)
        {
            PortalTower2D candidate = portals[i];
            if (!IsValidDestinationPortal(candidate))
                continue;

            float sqrDist = ((Vector2)candidate.GetAnchorWorldPosition() - myPos).sqrMagnitude;
            if (sqrDist <= closestSqrDist)
            {
                closestSqrDist = sqrDist;
                closest = candidate;
            }
        }

        return closest;
    }

    private bool IsValidDestinationPortal(PortalTower2D candidate)
    {
        if (candidate == null || candidate == this || !candidate.isActiveAndEnabled)
            return false;

        return string.IsNullOrEmpty(portalNetworkID) || candidate.portalNetworkID == portalNetworkID;
    }

    public Vector3 GetAnchorWorldPosition()
    {
        return portalAnchorPoint != null ? portalAnchorPoint.position : transform.position;
    }

    public Vector3 GetExitWorldPosition()
    {
        Transform anchor = portalAnchorPoint != null ? portalAnchorPoint : transform;
        return anchor.position + new Vector3(exitPositionOffset.x, exitPositionOffset.y, 0f);
    }

    public Vector3 GetVfxStartWorldPosition()
    {
        Transform basePoint = vfxStartPoint != null
            ? vfxStartPoint
            : (portalAnchorPoint != null ? portalAnchorPoint : transform);

        return basePoint.position + new Vector3(vfxStartPositionOffset.x, vfxStartPositionOffset.y, 0f);
    }

    #endregion

    #region VFX Graph Helpers

    private GameObject EnsurePersistentVfxActive()
    {
        Vector3 initialPos = GetVfxStartWorldPosition();

        if (activeVfxObject == null)
        {
            // 1. Check if teleportVfxGraphObject is assigned in the Inspector
            if (teleportVfxGraphObject != null)
            {
                if (teleportVfxGraphObject == gameObject)
                {
                    // Avoid moving the PortalTower2D root collider itself; extract its VisualEffectAsset onto a dedicated VFX GameObject
                    if (TryGetComponent(out VisualEffect rootVfx))
                    {
                        if (teleportVfxAsset == null && rootVfx.visualEffectAsset != null)
                            teleportVfxAsset = rootVfx.visualEffectAsset;

                        rootVfx.enabled = false;
                    }
                }
                else if (teleportVfxGraphObject.scene.IsValid())
                {
                    // Already a scene GameObject (e.g., child or scene VFX object) — use directly and keep active
                    activeVfxObject = teleportVfxGraphObject;
                }
                else if (Application.isPlaying)
                {
                    // Prefab asset — instantiate once and keep active permanently
                    activeVfxObject = Instantiate(teleportVfxGraphObject, initialPos, Quaternion.identity);
                    activeVfxObject.name = $"{name}_{teleportVfxGraphObject.name}";
                }
            }

            // 2. If still not resolved, check if a VisualEffect component exists on a child or on this GameObject
            if (activeVfxObject == null)
            {
                VisualEffect[] childVfxList = GetComponentsInChildren<VisualEffect>(true);
                for (int i = 0; i < childVfxList.Length; i++)
                {
                    VisualEffect candidateVfx = childVfxList[i];
                    if (candidateVfx == null)
                        continue;

                    if (candidateVfx.gameObject != gameObject)
                    {
                        activeVfxObject = candidateVfx.gameObject;
                        break;
                    }

                    if (teleportVfxAsset == null && candidateVfx.visualEffectAsset != null)
                    {
                        teleportVfxAsset = candidateVfx.visualEffectAsset;
                    }
                    candidateVfx.enabled = false;
                }
            }

            // 3. If a VisualEffectAsset is assigned, create the persistent VFX GameObject at the customizable VFX Start Position
            if (activeVfxObject == null && teleportVfxAsset != null && Application.isPlaying)
            {
                activeVfxObject = new GameObject($"{name}_PortalVFX");
            }

            if (activeVfxObject != null && m_activeTransports.Count == 0)
            {
                activeVfxObject.transform.position = initialPos;
            }
        }

        if (activeVfxObject != null)
        {
            ConfigureAndActivateVfxGraphs(activeVfxObject);
        }

        return activeVfxObject;
    }

    private void ConfigureAndActivateVfxGraphs(GameObject vfxObj)
    {
        if (vfxObj == null)
            return;

        if (!vfxObj.activeSelf)
            vfxObj.SetActive(true);

        VisualEffect[] vfxGraphs = vfxObj.GetComponentsInChildren<VisualEffect>(true);
        if (vfxGraphs == null || vfxGraphs.Length == 0)
        {
            VisualEffect addedVfx = vfxObj.AddComponent<VisualEffect>();
            vfxGraphs = new[] { addedVfx };
        }

        TryGetComponent(out SpriteRenderer portalSprite);

        for (int i = 0; i < vfxGraphs.Length; i++)
        {
            VisualEffect vfx = vfxGraphs[i];
            if (vfx == null)
                continue;

            if (!vfx.gameObject.activeSelf)
                vfx.gameObject.SetActive(true);

            vfx.enabled = true;

            if (vfx.visualEffectAsset == null && teleportVfxAsset != null)
            {
                vfx.visualEffectAsset = teleportVfxAsset;
            }

            if (portalSprite != null && vfx.TryGetComponent(out Renderer vfxRenderer))
            {
                if (vfxRenderer.sortingLayerID == 0 && vfxRenderer.sortingOrder == 0)
                {
                    vfxRenderer.sortingLayerID = portalSprite.sortingLayerID;
                    vfxRenderer.sortingOrder = portalSprite.sortingOrder + 1;
                }
            }

            if (!m_vfxInitialized)
            {
                vfx.Play();
            }
        }

        m_vfxInitialized = true;
    }

    private static void SetVfxComponentsEnabled(GameObject vfxObj, bool isEnabled)
    {
        if (vfxObj == null)
            return;

        VisualEffect[] vfxGraphs = vfxObj.GetComponentsInChildren<VisualEffect>(true);
        for (int i = 0; i < vfxGraphs.Length; i++)
        {
            VisualEffect vfx = vfxGraphs[i];
            if (vfx == null)
                continue;

            if (!isEnabled)
            {
                vfx.Reinit();
                vfx.enabled = false;
            }
            else
            {
                vfx.enabled = true;
                vfx.Reinit();
                vfx.Play();
            }
        }
    }

    #endregion

    #region Gizmos

    private void OnDrawGizmos()
    {
        if (showGizmos && !drawGizmosOnlyWhenSelected)
            DrawPortalGizmos();
    }

    private void OnDrawGizmosSelected()
    {
        if (showGizmos && drawGizmosOnlyWhenSelected)
            DrawPortalGizmos();
    }

    private void DrawPortalGizmos()
    {
        Vector3 anchorPos = GetAnchorWorldPosition();
        Vector3 exitPos = GetExitWorldPosition();
        Vector3 vfxStartPos = GetVfxStartWorldPosition();
        PortalTower2D closestPortal = FindClosestPortalInRange();

        // 1. Portal Link Range Circle
        Gizmos.color = closestPortal != null ? rangeGizmoColor : noLinkGizmoColor;
        DrawWireCircle2D(anchorPos, Mathf.Max(0f, maxPortalLinkRange), Mathf.Max(16, gizmoCircleSegments));

        // 2. Exit Position Gizmo
        Gizmos.color = exitPointGizmoColor;
        DrawWireCircle2D(exitPos, 0.14f, 20);
        Gizmos.DrawWireCube(exitPos, new Vector3(0.18f, 0.18f, 0.02f));
        Gizmos.DrawLine(anchorPos, exitPos);

        // 3. VFX Starting Position Gizmo (Circle + Inner Diamond + Crosshair + Anchor Line)
        Gizmos.color = vfxStartPointGizmoColor;
        DrawWireCircle2D(vfxStartPos, 0.18f, 24);
        DrawWireCircle2D(vfxStartPos, 0.06f, 12);
        const float crossSize = 0.22f;
        Gizmos.DrawLine(vfxStartPos + new Vector3(-crossSize, 0f, 0f), vfxStartPos + new Vector3(crossSize, 0f, 0f));
        Gizmos.DrawLine(vfxStartPos + new Vector3(0f, -crossSize, 0f), vfxStartPos + new Vector3(0f, crossSize, 0f));
        Gizmos.DrawLine(anchorPos, vfxStartPos);

        if (closestPortal != null)
        {
            Vector3 targetExitPos = closestPortal.GetExitWorldPosition();
            Vector3 targetVfxStartPos = closestPortal.GetVfxStartWorldPosition();

            // 4. Direct Link Line & Direction Arrow
            Gizmos.color = activeLinkGizmoColor;
            Gizmos.DrawLine(anchorPos, targetExitPos);

            Vector2 dir = (Vector2)(targetExitPos - anchorPos);
            if (dir.sqrMagnitude > 0.01f)
            {
                Vector3 midPoint = Vector3.Lerp(anchorPos, targetExitPos, 0.55f);
                Vector2 dirNorm = dir.normalized;
                Vector2 perp = new Vector2(-dirNorm.y, dirNorm.x);
                const float headLen = 0.35f;
                Gizmos.DrawLine(midPoint, midPoint - (Vector3)(dirNorm * headLen) + (Vector3)(perp * headLen * 0.55f));
                Gizmos.DrawLine(midPoint, midPoint - (Vector3)(dirNorm * headLen) - (Vector3)(perp * headLen * 0.55f));
            }

            // 5. VFX Travel Trajectory Preview (from this Portal's VFX Start Position to the Destination Portal's VFX Start Position)
            if (vfxTransportMode == VfxTransportMode.TravelToDestinationPortal)
            {
                Gizmos.color = vfxStartPointGizmoColor;
                const int arcSegments = 20;
                Vector3 prevArcPoint = vfxStartPos;
                for (int i = 1; i <= arcSegments; i++)
                {
                    float t = (float)i / arcSegments;
                    Vector3 linearPos = Vector3.Lerp(vfxStartPos, targetVfxStartPos, t);
                    float parabolaArc = 4f * t * (1f - t) * travelArcHeight;
                    Vector3 nextArcPoint = linearPos + new Vector3(0f, parabolaArc, 0f);
                    Gizmos.DrawLine(prevArcPoint, nextArcPoint);
                    prevArcPoint = nextArcPoint;
                }
            }
        }
    }

    private static void DrawWireCircle2D(Vector2 center, float radius, int segments)
    {
        if (radius <= 0f || segments < 3)
            return;

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

    #endregion
}
