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

    [Tooltip("If true, preserves the incoming object's Rigidbody2D velocity when reactivated at the destination portal.")]
    public bool preserveVelocityOnExit = true;

    [Tooltip("Additional impulse velocity applied to the object when it exits the destination portal.")]
    public Vector2 exitVelocityImpulse = Vector2.zero;

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

    private void Reset()
    {
        EnsureTriggerCollider();
    }

    private void OnValidate()
    {
        EnsureTriggerCollider();
    }

    private void Awake()
    {
        EnsureTriggerCollider();
    }

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

    private void EnsureTriggerCollider()
    {
        if (portalTriggerCollider == null)
            TryGetComponent(out portalTriggerCollider);

        if (portalTriggerCollider != null && !portalTriggerCollider.isTrigger)
        {
            portalTriggerCollider.isTrigger = true;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null) return;

        GameObject targetObj = ResolveTeleportableObject(other);
        if (targetObj == null) return;

        TryTeleportObject(targetObj);
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

        // Cache velocity before disabling the incoming GameObject
        Rigidbody2D rb = transportingObj.GetComponent<Rigidbody2D>();
        Vector2 savedVelocity = rb != null ? rb.linearVelocity : Vector2.zero;
        float savedAngularVelocity = rb != null ? rb.angularVelocity : 0f;

        onTeleportStarted?.Invoke(transportingObj, destinationPortal);

        // 1. Disable the incoming GameObject
        transportingObj.SetActive(false);

        // 2. Instantiate & enable the Particle System Prefab
        GameObject particleObj = SpawnParticlePrefab(startPos, transportingObj.name);
        ParticleSystem[] particleSystems = particleObj != null ? particleObj.GetComponentsInChildren<ParticleSystem>(true) : null;
        if (particleSystems != null)
        {
            for (int i = 0; i < particleSystems.Length; i++)
            {
                if (particleSystems[i] != null)
                {
                    particleSystems[i].gameObject.SetActive(true);
                    particleSystems[i].Play(true);
                }
            }
        }

        // 3. Wait / move Particle System Prefab from this Portal to the Destination Portal
        float duration = Mathf.Max(0.05f, teleportDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            // Re-evaluate destination exit position in case destination portal is moving
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

        // 4. Destroy the Particle System Prefab when teleportation is done
        if (particleObj != null)
        {
            if (particleDestroyDelay <= 0.001f)
            {
                Destroy(particleObj);
            }
            else
            {
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
        }

        // 5. Move transporting GameObject to destination portal, set cooldown, and reactivate it
        if (transportingObj != null)
        {
            if (destinationPortal != null)
            {
                endPos = destinationPortal.GetExitWorldPosition();
            }

            transportingObj.transform.position = endPos;
            s_objectCooldownUntil[transportingObj] = Time.time + Mathf.Max(0.05f, destinationPortal != null ? destinationPortal.reentryCooldown : reentryCooldown);

            transportingObj.SetActive(true);

            if (rb != null)
            {
                rb.linearVelocity = (preserveVelocityOnExit ? savedVelocity : Vector2.zero) +
                                    (destinationPortal != null ? destinationPortal.exitVelocityImpulse : exitVelocityImpulse);
                rb.angularVelocity = preserveVelocityOnExit ? savedAngularVelocity : 0f;
            }

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

    private GameObject SpawnParticlePrefab(Vector3 spawnPos, string targetName)
    {
        if (teleportParticlePrefab != null)
        {
            GameObject instance = Instantiate(teleportParticlePrefab, spawnPos, Quaternion.identity);
            instance.SetActive(true);
            return instance;
        }

        // Fallback runtime ParticleSystem if no prefab was assigned in the Inspector
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

    /// <summary>
    /// Finds the closest active PortalTower2D within maxPortalLinkRange.
    /// </summary>
    public PortalTower2D FindClosestPortalInRange()
    {
        Vector2 myPos = GetAnchorWorldPosition();
        float maxRangeSqr = maxPortalLinkRange * maxPortalLinkRange;

        if (linkedPortalOverride != null && linkedPortalOverride != this && linkedPortalOverride.isActiveAndEnabled)
        {
            float overrideSqrDist = ((Vector2)linkedPortalOverride.GetAnchorWorldPosition() - myPos).sqrMagnitude;
            if (overrideSqrDist <= maxRangeSqr)
            {
                return linkedPortalOverride;
            }
        }

        PortalTower2D closest = null;
        float closestSqrDist = maxRangeSqr;

        if (Application.isPlaying)
        {
            for (int i = 0; i < s_activePortals.Count; i++)
            {
                PortalTower2D candidate = s_activePortals[i];
                if (!IsValidDestinationPortal(candidate))
                    continue;

                float sqrDist = ((Vector2)candidate.GetAnchorWorldPosition() - myPos).sqrMagnitude;
                if (sqrDist <= closestSqrDist)
                {
                    closestSqrDist = sqrDist;
                    closest = candidate;
                }
            }
        }
        else
        {
            PortalTower2D[] scenePortals = FindObjectsByType<PortalTower2D>(FindObjectsSortMode.None);
            for (int i = 0; i < scenePortals.Length; i++)
            {
                PortalTower2D candidate = scenePortals[i];
                if (!IsValidDestinationPortal(candidate))
                    continue;

                float sqrDist = ((Vector2)candidate.GetAnchorWorldPosition() - myPos).sqrMagnitude;
                if (sqrDist <= closestSqrDist)
                {
                    closestSqrDist = sqrDist;
                    closest = candidate;
                }
            }
        }

        return closest;
    }

    private bool IsValidDestinationPortal(PortalTower2D candidate)
    {
        if (candidate == null || candidate == this || !candidate.isActiveAndEnabled)
            return false;

        if (!string.IsNullOrEmpty(portalNetworkID) && candidate.portalNetworkID != portalNetworkID)
            return false;

        return true;
    }

    private GameObject ResolveTeleportableObject(Collider2D col)
    {
        if (col == null) return null;

        GameObject candidate = col.attachedRigidbody != null ? col.attachedRigidbody.gameObject : col.gameObject;
        if (candidate == gameObject || candidate.transform.IsChildOf(transform) || transform.IsChildOf(candidate.transform))
            return null;

        // Check LayerMask on candidate or collider GameObject
        bool layerValid = ((teleportableLayers.value & (1 << candidate.layer)) != 0) ||
                          ((teleportableLayers.value & (1 << col.gameObject.layer)) != 0);
        if (!layerValid)
            return null;

        // If no tags are specified, allow any object matching teleportableLayers
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

    public Vector3 GetAnchorWorldPosition()
    {
        return portalAnchorPoint != null ? portalAnchorPoint.position : transform.position;
    }

    public Vector3 GetExitWorldPosition()
    {
        Transform anchor = portalAnchorPoint != null ? portalAnchorPoint : transform;
        return anchor.position + new Vector3(exitPositionOffset.x, exitPositionOffset.y, 0f);
    }

    private void OnDrawGizmos()
    {
        if (!showGizmos || drawGizmosOnlyWhenSelected)
            return;

        DrawPortalGizmos();
    }

    private void OnDrawGizmosSelected()
    {
        if (!showGizmos || !drawGizmosOnlyWhenSelected)
            return;

        DrawPortalGizmos();
    }

    private void DrawPortalGizmos()
    {
        Vector3 anchorPos = GetAnchorWorldPosition();
        Vector3 exitPos = GetExitWorldPosition();
        PortalTower2D closestPortal = FindClosestPortalInRange();

        // 1. Draw Portal Range Circle
        Gizmos.color = closestPortal != null ? rangeGizmoColor : noLinkGizmoColor;
        DrawWireCircle2D(anchorPos, Mathf.Max(0f, maxPortalLinkRange), Mathf.Max(16, gizmoCircleSegments));

        // 2. Draw Exit Position Offset Marker
        Gizmos.color = exitPointGizmoColor;
        DrawWireCircle2D(exitPos, 0.14f, 20);
        Gizmos.DrawWireCube(exitPos, new Vector3(0.18f, 0.18f, 0.02f));
        Gizmos.DrawLine(anchorPos, exitPos);

        // 3. Draw Link Line & Arrow to the Closest Portal in Range
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
                float headLen = 0.35f;
                Gizmos.DrawLine(midPoint, midPoint - (Vector3)(dirNorm * headLen) + (Vector3)(perp * headLen * 0.55f));
                Gizmos.DrawLine(midPoint, midPoint - (Vector3)(dirNorm * headLen) - (Vector3)(perp * headLen * 0.55f));
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

