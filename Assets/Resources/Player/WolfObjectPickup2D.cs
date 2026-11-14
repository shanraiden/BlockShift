using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class WolfObjectPickup2D : MonoBehaviour
{
    public enum CarryMode
    {
        [Tooltip("Parents the picked-up object directly to the Hold Point (e.g. Wolf's Mouth/Jaw bone) and sets its Rigidbody2D to Kinematic.")]
        ParentToHoldPoint,

        [Tooltip("Keeps the object unparented and pulls its Rigidbody2D toward the Hold Point using physics velocity.")]
        PhysicsFollowHoldPoint
    }

    public enum InputTriggerMode
    {
        [Tooltip("Press the button once to pick up, press again to drop.")]
        ToggleOnPress,

        [Tooltip("Hold the button to pick up and carry; release the button to drop.")]
        HoldToCarry
    }

    [Header("Layer & Detection Settings")]
    [Tooltip("Only GameObjects on these layers can be picked up by the Wolf.")]
    public LayerMask pickableLayer;

    [Tooltip("Optional tag filter. Leave empty to allow any GameObject on the pickableLayer.")]
    public string requiredTag = "";

    [Tooltip("Center Transform used to search for nearby pickable objects (defaults to Hold Point or this Transform).")]
    public Transform detectionOrigin;

    [Tooltip("Detection radius around Detection Origin to find pickable objects.")]
    public float pickupRadius = 1.25f;

    [Tooltip("Local offset from Detection Origin for the pickup search area.")]
    public Vector2 detectionOffset = Vector2.zero;

    [Header("Mouth / Hold Point Settings")]
    [Tooltip("The Transform (e.g. Wolf Mouth / Jaw Bone) where the picked-up object is held.")]
    public Transform holdPoint;

    [Tooltip("How the picked-up object is carried by the Wolf.")]
    public CarryMode carryMode = CarryMode.ParentToHoldPoint;

    [Tooltip("Local position offset relative to Hold Point while carried.")]
    public Vector2 holdLocalOffset = Vector2.zero;

    [Tooltip("Local Z rotation angle (in degrees) applied to the object while held.")]
    public float holdLocalRotationZ = 0f;

    [Tooltip("How fast the object snaps/moves into the Hold Point (0 = instant).")]
    public float snapSmoothSpeed = 22f;

    [Tooltip("If true, locks the object's Z rotation while carried.")]
    public bool lockRotationWhileHeld = true;

    [Tooltip("If true, ignores Physics2D collisions between the Wolf's colliders and the held object while carrying.")]
    public bool ignoreWolfCollisionsWhileHeld = true;

    [Tooltip("If true, disables the picked-up object's Collider2D entirely while held and re-enables it on drop.")]
    public bool disableObjectCollidersWhileHeld = false;

    [Header("Drop & Throw Settings")]
    [Tooltip("Local offset from Hold Point where the object is placed when dropped.")]
    public Vector2 dropPositionOffset = new Vector2(0.15f, 0f);

    [Tooltip("Velocity impulse applied when dropping the object (X flips automatically with Wolf facing direction).")]
    public Vector2 dropLaunchVelocity = new Vector2(2.0f, 1.0f);

    [Tooltip("How much of the Wolf's current Rigidbody2D velocity is inherited by the dropped object [0..1+].")]
    [Range(0f, 1.5f)]
    public float inheritWolfVelocityFactor = 0.75f;

    [Tooltip("Minimum delay (in seconds) between picking up and dropping.")]
    public float interactionCooldown = 0.15f;

    [Header("Input Settings (New Input System & UI)")]
    [Tooltip("How the pickup/drop input action behaves.")]
    public InputTriggerMode inputTriggerMode = InputTriggerMode.ToggleOnPress;

    [Tooltip("Input Action to Pick Up / Drop objects (defaults to E on Keyboard and Button West / X on Gamepad).")]
    public InputAction pickupDropAction = new InputAction("PickupOrDrop", InputActionType.Button);

    [Tooltip("Optional UI Button for mobile/touch pickup and drop.")]
    public Button pickupDropUIButton;

    [Header("References")]
    [Tooltip("Optional reference to the Wolf's Rigidbody2D (auto-detected if left empty).")]
    public Rigidbody2D wolfRigidbody;

    [Tooltip("If true, determines the Wolf's facing direction from transform.localScale.x < 0 (matching PlayerMovement).")]
    public bool facingRightWhenScaleXNegative = true;

    [Header("Events")]
    public UnityEvent<GameObject> onObjectPickedUp;
    public UnityEvent<GameObject> onObjectDropped;

    [Header("Gizmo Visualization Settings")]
    [Tooltip("Enable Scene/Game view Gizmos for the Detection Radius and Hold Point.")]
    public bool showGizmos = true;

    [Tooltip("If false, the Detection Radius Gizmo is always visible in the Scene View even when the Wolf is not selected.")]
    public bool drawGizmosOnlyWhenSelected = false;

    [Tooltip("Number of line segments used to draw the 2D Detection Radius circle.")]
    [Range(16, 96)]
    public int gizmoCircleSegments = 48;

    public Color idleRadiusColor = new Color(0.2f, 0.75f, 1f, 0.9f);
    public Color detectedRadiusColor = new Color(1f, 0.85f, 0.1f, 0.95f);
    public Color holdingRadiusColor = new Color(0.2f, 0.95f, 0.35f, 0.95f);
    public Color holdPointGizmoColor = new Color(1f, 0.25f, 0.85f, 0.95f);
    public Color dropPositionGizmoColor = new Color(1f, 0.45f, 0.1f, 0.95f);

    [Header("Status (Read-Only)")]
    [SerializeField] private GameObject currentHeldObject;
    [SerializeField] private GameObject nearestCandidateObject;

    // Cached state of the currently held object so it can be cleanly restored on drop
    private Transform heldOriginalParent;
    private Rigidbody2D heldRigidbody;
    private RigidbodyType2D heldOriginalBodyType;
    private float heldOriginalGravityScale;
    private RigidbodyConstraints2D heldOriginalConstraints;
    private bool heldOriginalSimulated;
    private Vector3 heldOriginalScale;
    private readonly List<Collider2D> heldObjectColliders = new List<Collider2D>();
    private readonly List<bool> heldCollidersOriginalEnabled = new List<bool>();
    private Collider2D[] wolfColliders;
    private float lastInteractionTime = -10f;
    private int lastKnownFacingDirection = -1; // -1 = Left, +1 = Right

    public bool IsHoldingObject => currentHeldObject != null;
    public GameObject CurrentHeldObject => currentHeldObject;
    public GameObject NearestCandidateObject => nearestCandidateObject;

    private void Awake()
    {
        if (wolfRigidbody == null)
            wolfRigidbody = GetComponentInParent<Rigidbody2D>();

        if (holdPoint == null)
            holdPoint = transform;

        if (detectionOrigin == null)
            detectionOrigin = holdPoint != null ? holdPoint : transform;

        wolfColliders = transform.root.GetComponentsInChildren<Collider2D>(true);

        // Populate default Keyboard & Gamepad bindings if none were assigned in the Inspector
        if (pickupDropAction.bindings.Count == 0)
        {
            pickupDropAction.AddBinding("<Keyboard>/e");
            pickupDropAction.AddBinding("<Keyboard>/f");
            pickupDropAction.AddBinding("<Gamepad>/buttonWest");
        }

        if (pickupDropUIButton != null)
        {
            pickupDropUIButton.onClick.AddListener(TogglePickupDrop);
        }
    }

    private void OnEnable()
    {
        pickupDropAction?.Enable();
    }

    private void OnDisable()
    {
        pickupDropAction?.Disable();
    }

    private void OnDestroy()
    {
        if (pickupDropUIButton != null)
        {
            pickupDropUIButton.onClick.RemoveListener(TogglePickupDrop);
        }
    }

    private void Update()
    {
        UpdateFacingDirection();
        nearestCandidateObject = currentHeldObject == null ? FindNearestPickableObject() : null;

        HandleInput();

        if (currentHeldObject != null && carryMode == CarryMode.ParentToHoldPoint)
        {
            UpdateParentedHoldTransform(Time.deltaTime);
        }
    }

    private void FixedUpdate()
    {
        if (currentHeldObject != null && carryMode == CarryMode.PhysicsFollowHoldPoint)
        {
            UpdatePhysicsHoldFollow(Time.fixedDeltaTime);
        }
    }

    private void HandleInput()
    {
        if (pickupDropAction == null) return;

        if (inputTriggerMode == InputTriggerMode.ToggleOnPress)
        {
            if (pickupDropAction.WasPressedThisFrame())
            {
                TogglePickupDrop();
            }
        }
        else // HoldToCarry
        {
            if (pickupDropAction.WasPressedThisFrame() && currentHeldObject == null)
            {
                TryPickupNearest();
            }
            else if (pickupDropAction.WasReleasedThisFrame() && currentHeldObject != null)
            {
                DropCurrentObject();
            }
        }
    }

    /// <summary>
    /// Toggles between picking up the nearest valid object on pickableLayer and dropping the currently held object.
    /// Can also be wired directly to UI Buttons.
    /// </summary>
    public void TogglePickupDrop()
    {
        if (Time.time - lastInteractionTime < interactionCooldown)
            return;

        if (currentHeldObject != null)
        {
            DropCurrentObject();
        }
        else
        {
            TryPickupNearest();
        }
    }

    /// <summary>
    /// Searches within pickupRadius for the closest GameObject on pickableLayer and picks it up.
    /// </summary>
    public bool TryPickupNearest()
    {
        if (currentHeldObject != null)
            return false;

        if (Time.time - lastInteractionTime < interactionCooldown)
            return false;

        GameObject candidate = FindNearestPickableObject();
        if (candidate == null)
            return false;

        return PickupObject(candidate);
    }

    /// <summary>
    /// Picks up a specific target GameObject if it belongs to pickableLayer.
    /// </summary>
    public bool PickupObject(GameObject target)
    {
        if (target == null || currentHeldObject != null)
            return false;

        if (!IsOnPickableLayer(target))
            return false;

        if (!string.IsNullOrEmpty(requiredTag) && !target.CompareTag(requiredTag))
            return false;

        currentHeldObject = target;
        lastInteractionTime = Time.time;

        // Cache original hierarchy and scale
        heldOriginalParent = target.transform.parent;
        heldOriginalScale = target.transform.lossyScale;

        // Cache and configure colliders
        heldObjectColliders.Clear();
        heldCollidersOriginalEnabled.Clear();
        target.GetComponentsInChildren(true, heldObjectColliders);

        for (int i = 0; i < heldObjectColliders.Count; i++)
        {
            Collider2D col = heldObjectColliders[i];
            if (col == null) continue;

            heldCollidersOriginalEnabled.Add(col.enabled);

            if (disableObjectCollidersWhileHeld)
            {
                col.enabled = false;
            }
            else if (ignoreWolfCollisionsWhileHeld && wolfColliders != null)
            {
                for (int w = 0; w < wolfColliders.Length; w++)
                {
                    if (wolfColliders[w] != null)
                    {
                        Physics2D.IgnoreCollision(col, wolfColliders[w], true);
                    }
                }
            }
        }

        // Cache and configure Rigidbody2D
        heldRigidbody = target.GetComponent<Rigidbody2D>();
        if (heldRigidbody != null)
        {
            heldOriginalBodyType = heldRigidbody.bodyType;
            heldOriginalGravityScale = heldRigidbody.gravityScale;
            heldOriginalConstraints = heldRigidbody.constraints;
            heldOriginalSimulated = heldRigidbody.simulated;

            heldRigidbody.linearVelocity = Vector2.zero;
            heldRigidbody.angularVelocity = 0f;

            if (carryMode == CarryMode.ParentToHoldPoint)
            {
                heldRigidbody.bodyType = RigidbodyType2D.Kinematic;
            }
            else
            {
                heldRigidbody.gravityScale = 0f;
                if (lockRotationWhileHeld)
                {
                    heldRigidbody.constraints = RigidbodyConstraints2D.FreezeRotation;
                }
            }
        }

        if (carryMode == CarryMode.ParentToHoldPoint && holdPoint != null)
        {
            target.transform.SetParent(holdPoint, worldPositionStays: true);
            if (snapSmoothSpeed <= 0f)
            {
                target.transform.localPosition = new Vector3(holdLocalOffset.x, holdLocalOffset.y, target.transform.localPosition.z);
                if (lockRotationWhileHeld)
                {
                    target.transform.localRotation = Quaternion.Euler(0f, 0f, holdLocalRotationZ);
                }
            }
        }

        onObjectPickedUp?.Invoke(currentHeldObject);
        return true;
    }

    /// <summary>
    /// Drops the currently held GameObject and restores its physics and collision state.
    /// </summary>
    public GameObject DropCurrentObject()
    {
        if (currentHeldObject == null)
            return null;

        GameObject droppedObj = currentHeldObject;
        currentHeldObject = null;
        lastInteractionTime = Time.time;

        // Restore original parent
        if (droppedObj.transform.parent == holdPoint)
        {
            droppedObj.transform.SetParent(heldOriginalParent, worldPositionStays: true);
        }

        // Fix any negative scale inherited while parented to a flipped rig
        Vector3 currentLocalScale = droppedObj.transform.localScale;
        if (Mathf.Sign(currentLocalScale.x) != Mathf.Sign(heldOriginalScale.x))
        {
            currentLocalScale.x = Mathf.Abs(currentLocalScale.x) * Mathf.Sign(heldOriginalScale.x);
            droppedObj.transform.localScale = currentLocalScale;
        }

        // Apply drop offset in facing direction
        droppedObj.transform.position = GetDropWorldPosition();

        // Restore colliders and re-enable collisions with the Wolf
        for (int i = 0; i < heldObjectColliders.Count; i++)
        {
            Collider2D col = heldObjectColliders[i];
            if (col == null) continue;

            if (i < heldCollidersOriginalEnabled.Count)
            {
                col.enabled = heldCollidersOriginalEnabled[i];
            }

            if (ignoreWolfCollisionsWhileHeld && wolfColliders != null)
            {
                for (int w = 0; w < wolfColliders.Length; w++)
                {
                    if (wolfColliders[w] != null)
                    {
                        Physics2D.IgnoreCollision(col, wolfColliders[w], false);
                    }
                }
            }
        }

        // Restore Rigidbody2D and apply release velocity
        if (heldRigidbody != null)
        {
            heldRigidbody.bodyType = heldOriginalBodyType;
            heldRigidbody.gravityScale = heldOriginalGravityScale;
            heldRigidbody.constraints = heldOriginalConstraints;
            heldRigidbody.simulated = heldOriginalSimulated;

            if (heldRigidbody.bodyType == RigidbodyType2D.Dynamic)
            {
                Vector2 wolfVel = wolfRigidbody != null ? wolfRigidbody.linearVelocity : Vector2.zero;
                Vector2 launchVel = new Vector2(
                    dropLaunchVelocity.x * lastKnownFacingDirection,
                    dropLaunchVelocity.y
                );
                heldRigidbody.linearVelocity = (wolfVel * inheritWolfVelocityFactor) + launchVel;
            }
        }

        heldRigidbody = null;
        heldObjectColliders.Clear();
        heldCollidersOriginalEnabled.Clear();

        onObjectDropped?.Invoke(droppedObj);
        return droppedObj;
    }

    /// <summary>
    /// Finds the closest valid GameObject on pickableLayer within pickupRadius.
    /// </summary>
    public GameObject FindNearestPickableObject()
    {
        Vector2 center = GetDetectionWorldCenter();
        Collider2D[] hits = Physics2D.OverlapCircleAll(center, pickupRadius, pickableLayer);

        if (hits == null || hits.Length == 0)
            return null;

        GameObject closestObj = null;
        float closestSqrDist = float.MaxValue;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null) continue;

            // Prefer the Rigidbody2D root GameObject if the collider is on a child object
            GameObject candidate = hit.attachedRigidbody != null ? hit.attachedRigidbody.gameObject : hit.gameObject;

            // Skip self or children of the Wolf
            if (candidate.transform.IsChildOf(transform.root))
                continue;

            if (!IsOnPickableLayer(candidate) && !IsOnPickableLayer(hit.gameObject))
                continue;

            if (!string.IsNullOrEmpty(requiredTag) && !candidate.CompareTag(requiredTag))
                continue;

            Vector2 closestPoint = hit.ClosestPoint(center);
            float sqrDist = (closestPoint - center).sqrMagnitude;

            if (sqrDist < closestSqrDist)
            {
                closestSqrDist = sqrDist;
                closestObj = candidate;
            }
        }

        return closestObj;
    }

    public bool IsOnPickableLayer(GameObject obj)
    {
        if (obj == null) return false;
        return (pickableLayer.value & (1 << obj.layer)) != 0;
    }

    private void UpdateParentedHoldTransform(float dt)
    {
        if (currentHeldObject == null || holdPoint == null)
            return;

        Vector3 targetLocalPos = new Vector3(
            holdLocalOffset.x,
            holdLocalOffset.y,
            currentHeldObject.transform.localPosition.z
        );

        Quaternion targetLocalRot = Quaternion.Euler(0f, 0f, holdLocalRotationZ);

        if (snapSmoothSpeed <= 0f)
        {
            currentHeldObject.transform.localPosition = targetLocalPos;
            if (lockRotationWhileHeld)
                currentHeldObject.transform.localRotation = targetLocalRot;
        }
        else
        {
            float t = 1f - Mathf.Exp(-snapSmoothSpeed * dt);
            currentHeldObject.transform.localPosition = Vector3.Lerp(currentHeldObject.transform.localPosition, targetLocalPos, t);
            if (lockRotationWhileHeld)
                currentHeldObject.transform.localRotation = Quaternion.Slerp(currentHeldObject.transform.localRotation, targetLocalRot, t);
        }
    }

    private void UpdatePhysicsHoldFollow(float fixedDt)
    {
        if (currentHeldObject == null || holdPoint == null || fixedDt <= 0f)
            return;

        Vector3 targetWorldPos = holdPoint.TransformPoint(new Vector3(holdLocalOffset.x, holdLocalOffset.y, 0f));

        if (heldRigidbody != null)
        {
            Vector2 toTarget = (Vector2)targetWorldPos - heldRigidbody.position;
            float followSpeed = snapSmoothSpeed > 0f ? snapSmoothSpeed : 25f;
            heldRigidbody.linearVelocity = toTarget * followSpeed;

            if (lockRotationWhileHeld)
            {
                float targetAngle = holdPoint.eulerAngles.z + holdLocalRotationZ;
                heldRigidbody.MoveRotation(Mathf.LerpAngle(heldRigidbody.rotation, targetAngle, 1f - Mathf.Exp(-followSpeed * fixedDt)));
            }
        }
        else
        {
            currentHeldObject.transform.position = Vector3.Lerp(
                currentHeldObject.transform.position,
                targetWorldPos,
                1f - Mathf.Exp(-Mathf.Max(1f, snapSmoothSpeed) * fixedDt)
            );
        }
    }

    private void UpdateFacingDirection()
    {
        if (wolfRigidbody != null && Mathf.Abs(wolfRigidbody.linearVelocity.x) > 0.05f)
        {
            lastKnownFacingDirection = wolfRigidbody.linearVelocity.x > 0f ? 1 : -1;
            return;
        }

        Transform rootTransform = transform.root != null ? transform.root : transform;
        float scaleX = rootTransform.localScale.x;
        if (Mathf.Abs(scaleX) > 0.0001f)
        {
            if (facingRightWhenScaleXNegative)
                lastKnownFacingDirection = scaleX < 0f ? 1 : -1;
            else
                lastKnownFacingDirection = scaleX > 0f ? 1 : -1;
        }
    }

    private Vector2 GetDetectionWorldCenter()
    {
        Transform origin = detectionOrigin != null ? detectionOrigin : (holdPoint != null ? holdPoint : transform);
        return (Vector2)origin.TransformPoint(new Vector3(detectionOffset.x, detectionOffset.y, 0f));
    }

    private Vector3 GetDropWorldPosition()
    {
        UpdateFacingDirection();
        Transform activeHoldPoint = holdPoint != null ? holdPoint : transform;
        Vector3 dropWorldOffset = new Vector3(
            dropPositionOffset.x * lastKnownFacingDirection,
            dropPositionOffset.y,
            0f
        );
        return activeHoldPoint.position + dropWorldOffset;
    }

    private void OnDrawGizmos()
    {
        if (!showGizmos || drawGizmosOnlyWhenSelected)
            return;

        DrawPickupGizmos();
    }

    private void OnDrawGizmosSelected()
    {
        if (!showGizmos || !drawGizmosOnlyWhenSelected)
            return;

        DrawPickupGizmos();
    }

    private void DrawPickupGizmos()
    {
        Vector2 detectionCenter = GetDetectionWorldCenter();
        GameObject candidate = Application.isPlaying ? nearestCandidateObject : FindNearestPickableObject();

        Color activeRadiusColor = currentHeldObject != null
            ? holdingRadiusColor
            : (candidate != null ? detectedRadiusColor : idleRadiusColor);

        // 1. Draw 2D Detection Radius Circle + subtle inner ring & center crosshair
        Gizmos.color = activeRadiusColor;
        DrawWireCircle2D(detectionCenter, pickupRadius, Mathf.Max(16, gizmoCircleSegments));
        Gizmos.DrawWireSphere(detectionCenter, pickupRadius);

        float crossSize = Mathf.Min(0.12f, pickupRadius * 0.15f);
        Gizmos.DrawLine(
            new Vector3(detectionCenter.x - crossSize, detectionCenter.y, 0f),
            new Vector3(detectionCenter.x + crossSize, detectionCenter.y, 0f)
        );
        Gizmos.DrawLine(
            new Vector3(detectionCenter.x, detectionCenter.y - crossSize, 0f),
            new Vector3(detectionCenter.x, detectionCenter.y + crossSize, 0f)
        );

        // 2. If a valid pickable candidate is inside the Detection Radius, draw a line & marker to it
        if (currentHeldObject == null && candidate != null)
        {
            Gizmos.color = detectedRadiusColor;
            Gizmos.DrawLine(detectionCenter, candidate.transform.position);
            DrawWireCircle2D(candidate.transform.position, 0.12f, 20);
        }

        // 3. Draw Hold Point (Mouth/Jaw carry socket) marker
        Transform activeHoldPoint = holdPoint != null ? holdPoint : transform;
        Vector3 holdWorld = activeHoldPoint.TransformPoint(new Vector3(holdLocalOffset.x, holdLocalOffset.y, 0f));
        Gizmos.color = holdPointGizmoColor;
        DrawWireCircle2D(holdWorld, 0.07f, 16);
        Gizmos.DrawWireSphere(holdWorld, 0.05f);
        Gizmos.DrawLine(activeHoldPoint.position, holdWorld);

        // 4. Draw Drop Position Offset marker, connecting line from Hold Point, and launch velocity arrow
        Vector3 dropWorld = GetDropWorldPosition();
        Gizmos.color = dropPositionGizmoColor;
        DrawWireCircle2D(dropWorld, 0.09f, 20);
        Gizmos.DrawWireCube(dropWorld, new Vector3(0.12f, 0.12f, 0.02f));
        Gizmos.DrawLine(holdWorld, dropWorld);

        float dropCross = 0.06f;
        Gizmos.DrawLine(dropWorld + new Vector3(-dropCross, 0f, 0f), dropWorld + new Vector3(dropCross, 0f, 0f));
        Gizmos.DrawLine(dropWorld + new Vector3(0f, -dropCross, 0f), dropWorld + new Vector3(0f, dropCross, 0f));

        // Draw directional launch velocity preview arrow from the Drop Position
        Vector2 launchDir = new Vector2(dropLaunchVelocity.x * lastKnownFacingDirection, dropLaunchVelocity.y);
        if (launchDir.sqrMagnitude > 0.001f)
        {
            Vector3 arrowEnd = dropWorld + (Vector3)(launchDir * 0.15f);
            Gizmos.DrawLine(dropWorld, arrowEnd);

            Vector2 dirNorm = launchDir.normalized;
            Vector2 perp = new Vector2(-dirNorm.y, dirNorm.x);
            float headLen = 0.07f;
            Vector3 headLeft = arrowEnd - (Vector3)(dirNorm * headLen) + (Vector3)(perp * headLen * 0.6f);
            Vector3 headRight = arrowEnd - (Vector3)(dirNorm * headLen) - (Vector3)(perp * headLen * 0.6f);
            Gizmos.DrawLine(arrowEnd, headLeft);
            Gizmos.DrawLine(arrowEnd, headRight);
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

