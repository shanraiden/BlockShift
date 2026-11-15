using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider2D))]
public class PortalTower2D : MonoBehaviour
{
    public enum ParticleTransportMode
    {
        [Tooltip("The spawned Particle System Prefab travels smoothly from this Portal to the closest destination Portal before being destroyed.")]
        TravelToDestinationPortal,

        [Tooltip("The spawned Particle System Prefab plays at the portal for Teleport Duration, then is destroyed as the object reactivates at the destination.")]
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

    [Tooltip("Transform point where teleported objects and particles enter/exit this portal (defaults to this Transform).")]
    public Transform portalAnchorPoint;

    [Tooltip("Local offset from Portal Anchor Point where arriving objects appear.")]
    public Vector2 exitPositionOffset = Vector2.zero;

    [Header("Teleportable Object Filtering")]
    [Tooltip("LayerMask of objects allowed to enter the portal.")]
    public LayerMask teleportableLayers = ~0;

    [Tooltip("Only GameObjects with one of these tags (on themselves or their root) can be teleported. Leave empty to allow any object on Teleportable Layers.")]
    public List<string> teleportableTags = new List<string> { "Player" };

    [Header("Particle Prefab & Teleportation Timing")]
    [Tooltip("Prefab with a ParticleSystem that is spawned/enabled when an object enters the portal and destroyed when teleportation finishes.")]
    public GameObject teleportParticlePrefab;

    [Tooltip("How the Particle System Prefab behaves while transporting the disabled object.")]
    public ParticleTransportMode particleTransportMode = ParticleTransportMode.TravelToDestinationPortal;

    [Tooltip("Time (in seconds) the teleportation takes while the incoming object is disabled and the Particle Prefab is active.")]
    [Min(0.05f)]
    public float teleportDuration = 0.65f;

    [Tooltip("Optional arc height (in world units) for the Particle Prefab's path when traveling to the destination portal (0 = straight line).")]
    public float travelArcHeight = 0.5f;

    [Tooltip("Extra delay (in seconds) before calling Destroy() on the Particle Prefab after the object reactivates so trailing particles can finish.")]
    [Min(0f)]
    public float particleDestroyDelay = 0f;

    [Header("Exit Physics & Cooldown")]
    [Tooltip("Cooldown (in seconds) after arriving at a destination portal before the object can trigger another teleport.")]
    public float reentryCooldown = 0.6f;

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

    [Header("Status (Read-Only)")]
    [SerializeField] private PortalTower2D currentClosestPortal;
    [SerializeField] private GameObject currentlyTransportingObject;

    private Collider2D portalTriggerCollider;
    private readonly HashSet<GameObject> m_activeTransports = new HashSet<GameObject>();

    public PortalTower2D ClosestLinkedPortal => FindClosestPortalInRange();

    #region Unity Lifecycle

    private void Reset() => EnsureTriggerCollider();
    private void OnValidate() => EnsureTriggerCollider();
    private void Awake() => EnsureTriggerCollider();

    private void OnEnable()
    {
        if (!s_activePortals.Contains(this))
            s_activePortals.Add(this);
    }

    private void OnDisable()
    {
        s_activePortals.Remove(this);
    }

    private void Update()
    {
        currentClosestPortal = FindClosestPortalInRange();
    }

    private void OnTriggerEnter2D(Collider2D other) => HandleTrigger(other);
    private void OnTriggerStay2D(Collider2D other) => HandleTrigger(other);

    #endregion

    #region Trigger & Teleportation Logic

    private void EnsureTriggerCollider()
    {
        if (portalTriggerCollider == null)
            TryGetComponent(out portalTriggerCollider);

        if (portalTriggerCollider != null && !portalTriggerCollider.isTrigger)
            portalTriggerCollider.isTrigger = true;
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
        currentlyTransportingObject = transportingObj;

        Vector3 startPos = GetAnchorWorldPosition();
        Vector3 endPos = destinationPortal.GetExitWorldPosition();

        Rigidbody2D rb = transportingObj.GetComponent<Rigidbody2D>();
        ResetRigidbodyVelocity(rb);

        onTeleportStarted?.Invoke(transportingObj, destinationPortal);

        // 1. Disable incoming GameObject
        transportingObj.SetActive(false);

        // 2. Spawn & play the Particle System Prefab
        GameObject particleObj = SpawnParticlePrefab(startPos, transportingObj.name);
        ParticleSystem[] particleSystems = PlayAllParticleSystems(particleObj);

        // 3. Move Particle System Prefab from this Portal to the Destination Portal
        float duration = Mathf.Max(0.05f, teleportDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float smoothT = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));

            if (destinationPortal != null)
            {
                endPos = destinationPortal.GetExitWorldPosition();
            }

            if (particleObj != null && particleTransportMode == ParticleTransportMode.TravelToDestinationPortal)
            {
                Vector3 linearPos = Vector3.Lerp(startPos, endPos, smoothT);
                float parabolaArc = 4f * smoothT * (1f - smoothT) * travelArcHeight;
                particleObj.transform.position = linearPos + new Vector3(0f, parabolaArc, 0f);
            }

            yield return null;
        }

        // 4. Destroy the Particle System Prefab
        CleanupParticlePrefab(particleObj, particleSystems);

        // 5. Move transporting GameObject to destination portal, reactivate, and zero velocity
        if (transportingObj != null)
        {
            if (destinationPortal != null)
            {
                endPos = destinationPortal.GetExitWorldPosition();
            }

            float cooldownDuration = destinationPortal != null ? destinationPortal.reentryCooldown : reentryCooldown;
            float cooldownUntil = Time.time + Mathf.Max(0.05f, cooldownDuration);

            transportingObj.transform.position = endPos;
            s_objectCooldownUntil[transportingObj] = cooldownUntil;
            transportingObj.SetActive(true);

            ResetRigidbodyVelocity(rb);

            Physics2D.SyncTransforms();

            onTeleportCompleted?.Invoke(transportingObj, destinationPortal);
            if (destinationPortal != null)
            {
                destinationPortal.onTeleportCompleted?.Invoke(transportingObj, this);
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

        bool layerValid = IsInLayerMask(candidate.layer, teleportableLayers) ||
                          IsInLayerMask(col.gameObject.layer, teleportableLayers);
        if (!layerValid)
            return null;

        if (teleportableTags == null || teleportableTags.Count == 0)
            return candidate;

        Transform root = candidate.transform.root;
        for (int i = 0; i < teleportableTags.Count; i++)
        {
            string allowedTag = teleportableTags[i];
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

    #endregion

    #region Particle System Helpers

    private GameObject SpawnParticlePrefab(Vector3 spawnPos, string targetName)
    {
        if (teleportParticlePrefab != null)
        {
            GameObject instance = Instantiate(teleportParticlePrefab, spawnPos, Quaternion.identity);
            instance.SetActive(true);
            return instance;
        }

        GameObject fallbackObj = new GameObject($"{targetName}_PortalTeleportParticles");
        fallbackObj.transform.position = spawnPos;

        ParticleSystem ps = fallbackObj.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true;
        main.startLifetime = 0.45f;
        main.startSpeed = 1.5f;
        main.startSize = 0.22f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.rateOverTime = 35f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.25f;

        return fallbackObj;
    }

    private static ParticleSystem[] PlayAllParticleSystems(GameObject particleObj)
    {
        if (particleObj == null)
            return null;

        ParticleSystem[] systems = particleObj.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < systems.Length; i++)
        {
            if (systems[i] != null)
            {
                systems[i].gameObject.SetActive(true);
                systems[i].Play(true);
            }
        }

        return systems;
    }

    private void CleanupParticlePrefab(GameObject particleObj, ParticleSystem[] particleSystems)
    {
        if (particleObj == null)
            return;

        if (particleDestroyDelay <= 0.001f)
        {
            Destroy(particleObj);
            return;
        }

        if (particleSystems != null)
        {
            for (int i = 0; i < particleSystems.Length; i++)
            {
                if (particleSystems[i] != null)
                    particleSystems[i].Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        Destroy(particleObj, particleDestroyDelay);
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
        PortalTower2D closestPortal = FindClosestPortalInRange();

        Gizmos.color = closestPortal != null ? rangeGizmoColor : noLinkGizmoColor;
        DrawWireCircle2D(anchorPos, Mathf.Max(0f, maxPortalLinkRange), Mathf.Max(16, gizmoCircleSegments));

        Gizmos.color = exitPointGizmoColor;
        DrawWireCircle2D(exitPos, 0.14f, 20);
        Gizmos.DrawWireCube(exitPos, new Vector3(0.18f, 0.18f, 0.02f));
        Gizmos.DrawLine(anchorPos, exitPos);

        if (closestPortal != null)
        {
            Vector3 targetExitPos = closestPortal.GetExitWorldPosition();
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
