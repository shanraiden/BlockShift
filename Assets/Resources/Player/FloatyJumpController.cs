using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

[RequireComponent(typeof(Rigidbody2D))]
public class FloatyJumpController : MonoBehaviour
{
    public enum AirRotationMode
    {
        VelocityTilt,
        ContinuousFlipSpin
    }

    [Header("Jump Settings")]
    public float jumpForce = 10f;
    [Tooltip("False = 1 Jump only (ground jump). True = 2 Jumps (ground jump + 1 air jump).")]
    public bool allowTwoJumps = true;

    [Tooltip("Minimum custom timer / gap (in seconds) required between multiple jumps.")]
    [Min(0f)]
    public float timeBetweenJumps = 0.2f;

    [Tooltip("Extra downward force applied while airborne for snappier jump/fall feel.")]
    [Min(0f)]
    public float downwardForce = 15f;

    [Tooltip("If true, applies Downward Force only while falling (linearVelocity.y < 0). If false, applies continuously while airborne.")]
    public bool applyDownwardForceOnlyWhenFalling = true;

    [Header("Horizontal Jump Velocity")]
    [Tooltip("Multiplier applied to the current velocity.x when jumping horizontally (1 = keep current velocity.x, > 1 = horizontal jump boost).")]
    [Min(0f)]
    public float horizontalJumpVelocityMultiplier = 1.25f;

    [Tooltip("Optional extra horizontal speed added in the direction of current velocity.x when horizontal jumping.")]
    public float extraHorizontalJumpSpeed = 0f;

    [Tooltip("If true, preserves and carries the captured horizontal jump velocity.x while airborne.")]
    public bool maintainHorizontalVelocityInAir = true;

    [Header("Jump Child Visual (Auto-finds child named 'Jump')")]
    [Tooltip("Child GameObject named 'Jump' containing the Jump SpriteRenderer. Auto-assigned from children if left empty.")]
    public GameObject jumpVisualObject;

    [Tooltip("Deactivate 'Jump' and keep other children active while grounded.")]
    public bool hideJumpVisualWhenGrounded = true;

    [Header("Jump Particle System")]
    [Tooltip("ParticleSystem GameObject (child/scene object or prefab) that plays while jumping.")]
    public GameObject jumpParticleObject;

    [Tooltip("Local position offset applied when spawning/positioning Jump Particle Object.")]
    public Vector2 jumpParticleLocalOffset = Vector2.zero;

    [Tooltip("If true, stops emitting jump particles when the player lands back on ground.")]
    public bool stopJumpParticlesOnLand = true;

    [Header("Smooth Air Rotation Settings")]
    public AirRotationMode airRotationMode = AirRotationMode.VelocityTilt;

    [FormerlySerializedAs("spinDegreesPerSecond")]
    [Tooltip("Rotational speed in degrees per second (spin rate in ContinuousFlipSpin mode, and max angular speed in VelocityTilt mode).")]
    public float rotationalSpeed = 360f;

    [Tooltip("How smoothly the Jump child eases toward its target rotation in the air.")]
    [Min(0.1f)]
    public float airRotationSmoothSpeed = 12f;

    [Tooltip("Tilt angle (degrees) when rising at full upward speed (VelocityTilt mode).")]
    public float maxUpwardTiltAngle = 22f;

    [Tooltip("Tilt angle (degrees) when falling at full downward speed (VelocityTilt mode).")]
    public float maxDownwardTiltAngle = -28f;

    [Tooltip("Vertical speed used to normalize upward/downward tilt angles (VelocityTilt mode).")]
    [Min(0.5f)]
    public float referenceVerticalSpeed = 10f;

    [Tooltip("Automatically flips the Jump SpriteRenderer X and tilt sign based on horizontal movement direction.")]
    public bool flipJumpVisualByHorizontalVelocity = true;

    [Header("Ground Check Settings (Surface Normal Based)")]
    public Transform groundCheck;
    public LayerMask groundLayer;
    public float groundCheckRadius = 0.3f;
    [Tooltip("Local position offset applied to the Ground Check Radius sphere (relative to Ground Check or this Transform).")]
    public Vector2 groundCheckLocalOffset = Vector2.zero;
    [Tooltip("Maximum surface separation distance (from collider skin to ground) treated as grounded.")]
    [Min(0.01f)]
    public float groundCheckDistance = 0.08f;

    [Tooltip("If false, accepts ground contact normals from any orientation (walls/ceilings/tilted ground, not locked to -Y). If true, filters against -Physics2D.gravity.")]
    public bool filterNormalByGravity = false;

    [Range(-1f, 1f)]
    [Tooltip("Minimum dot product between ground surface normal and -Physics2D.gravity when Filter Normal By Gravity is enabled.")]
    public float minGravityNormalDot = 0.45f;

    [Tooltip("If true, launches along the detected ground normal instead of purely along +Y.")]
    public bool jumpAlongGroundNormal = false;

    [Header("Gizmos")]
    public bool showGizmos = true;
    public float normalGizmoLength = 1.0f;

    [Header("Custom Jump Input (New Input System)")]
    public InputAction jumpAction = new InputAction(
        name: "Jump",
        type: InputActionType.Button,
        binding: "<Keyboard>/space"
    );

    public static bool isGrounded = false;

    private Rigidbody2D playerRigidBody;
    private Animator animator;
    private Collider2D playerCollider;
    private SpriteRenderer jumpSpriteRenderer;
    private Quaternion m_initialJumpVisualRotation = Quaternion.identity;
    private bool m_isJumpVisualActive;
    private float m_currentAirAngle;
    private float m_facingSign = 1f;
    private float m_capturedJumpVx;
    private bool m_hasCapturedHorizontalJumpVx;

    private readonly Dictionary<GameObject, bool> m_savedChildStates = new Dictionary<GameObject, bool>();
    private GameObject m_activeJumpParticleInstance;
    private ParticleSystem[] m_jumpParticleSystems;
    private bool hasUsedAirJump;
    private float lastJumpTime = float.NegativeInfinity;
    private Vector2 m_lastGroundNormal = Vector2.up;
    private Vector2 m_lastGroundPoint;
    private bool m_hasGroundHit;
    private readonly ContactPoint2D[] m_contacts = new ContactPoint2D[16];

    private void Reset()
    {
        ResolveJumpVisualObject();
    }

    private void Awake()
    {
        playerRigidBody = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        playerCollider = GetComponent<Collider2D>();

        ResolveJumpVisualObject();
        if (hideJumpVisualWhenGrounded && jumpVisualObject != null)
        {
            jumpVisualObject.SetActive(false);
        }

        InitializeJumpParticleObject();
    }

    private void ResolveJumpVisualObject()
    {
        if (jumpVisualObject == null)
        {
            Transform found = transform.Find("Jump");
            if (found == null)
            {
                Transform[] descendants = GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < descendants.Length; i++)
                {
                    if (descendants[i] != transform && descendants[i].name == "Jump")
                    {
                        found = descendants[i];
                        break;
                    }
                }
            }

            if (found != null)
            {
                jumpVisualObject = found.gameObject;
            }
        }

        if (jumpVisualObject != null)
        {
            jumpSpriteRenderer = jumpVisualObject.GetComponentInChildren<SpriteRenderer>(true);
            m_initialJumpVisualRotation = jumpVisualObject.transform.localRotation;
        }
    }

    private void OnEnable()
    {
        if (jumpAction != null)
        {
            if (jumpAction.bindings.Count == 0)
            {
                jumpAction.AddBinding("<Keyboard>/space");
            }
            jumpAction.performed += PlayerJumped;
            jumpAction.Enable();
        }
    }

    private void OnDisable()
    {
        if (jumpAction != null)
        {
            jumpAction.performed -= PlayerJumped;
            jumpAction.Disable();
        }

        RestoreGroundedChildVisuals();
        StopJumpParticles();
    }

    private void PlayerJumped(InputAction.CallbackContext context)
    {
        if (playerRigidBody == null)
            return;

        if (Time.time - lastJumpTime < Mathf.Max(0f, timeBetweenJumps))
            return;

        bool groundedNow = isGrounded || CheckIsGrounded();

        if (groundedNow)
        {
            hasUsedAirJump = false;
            isGrounded = false;
            TriggerJump();
            return;
        }

        if (allowTwoJumps && !hasUsedAirJump)
        {
            hasUsedAirJump = true;
            TriggerJump();
        }
    }

    private void TriggerJump()
    {
        lastJumpTime = Time.time;

        float currentVx = playerRigidBody.linearVelocity.x;
        bool isMovingHorizontally = Mathf.Abs(currentVx) > 0.05f;

        if (animator != null)
        {
            if (!isMovingHorizontally && animator.GetCurrentAnimatorStateInfo(0).IsName("Stand"))
            {
                animator.SetTrigger("VerticalJump");
            }
            else if (isMovingHorizontally)
            {
                animator.SetTrigger("LateralJump");
            }
        }

        float jumpVx = currentVx;
        if (isMovingHorizontally)
        {
            jumpVx = (currentVx * horizontalJumpVelocityMultiplier) + (Mathf.Sign(currentVx) * extraHorizontalJumpSpeed);
            m_capturedJumpVx = jumpVx;
            m_hasCapturedHorizontalJumpVx = true;
        }
        else
        {
            m_hasCapturedHorizontalJumpVx = false;
        }

        if (jumpAlongGroundNormal && m_hasGroundHit && m_lastGroundNormal.sqrMagnitude > 0.001f)
        {
            Vector2 normal = m_lastGroundNormal.normalized;
            Vector2 tangent = new Vector2(normal.y, -normal.x);
            float tangentSpeed = Vector2.Dot(new Vector2(jumpVx, 0f), tangent);
            playerRigidBody.linearVelocity = (tangent * tangentSpeed) + (normal * jumpForce);
        }
        else
        {
            playerRigidBody.linearVelocity = new Vector2(jumpVx, jumpForce);
        }

        ActivateJumpChildVisual();
        PlayJumpParticles();
    }

    private void Update()
    {
        UpdateAirRotation(Time.deltaTime);
    }

    private void FixedUpdate()
    {
        isGrounded = CheckIsGrounded();

        if (isGrounded && Time.time - lastJumpTime >= 0.08f)
        {
            hasUsedAirJump = false;
            m_hasCapturedHorizontalJumpVx = false;
            if (m_isJumpVisualActive)
            {
                RestoreGroundedChildVisuals();
            }
            if (stopJumpParticlesOnLand)
            {
                StopJumpParticles();
            }
        }
        else if (!isGrounded && !m_isJumpVisualActive)
        {
            ActivateJumpChildVisual();
        }

        if (!isGrounded && playerRigidBody != null)
        {
            if (maintainHorizontalVelocityInAir && m_hasCapturedHorizontalJumpVx)
            {
                float currentVx = playerRigidBody.linearVelocity.x;
                if (Mathf.Abs(currentVx) > Mathf.Abs(m_capturedJumpVx) || (currentVx * m_capturedJumpVx < -0.1f))
                {
                    m_capturedJumpVx = currentVx;
                }
                else
                {
                    playerRigidBody.linearVelocity = new Vector2(m_capturedJumpVx, playerRigidBody.linearVelocity.y);
                }
            }

            if (downwardForce > 0f && (!applyDownwardForceOnlyWhenFalling || playerRigidBody.linearVelocity.y < 0f))
            {
                playerRigidBody.AddForce(Vector2.down * downwardForce, ForceMode2D.Force);
            }
        }

        if (animator != null)
        {
            animator.SetBool("isGounded", isGrounded);
        }
    }

    private void ActivateJumpChildVisual()
    {
        ResolveJumpVisualObject();
        if (jumpVisualObject == null || m_isJumpVisualActive)
            return;

        m_savedChildStates.Clear();
        int childCount = transform.childCount;
        for (int i = 0; i < childCount; i++)
        {
            GameObject childObj = transform.GetChild(i).gameObject;
            if (childObj == jumpVisualObject || jumpVisualObject.transform.IsChildOf(childObj.transform))
                continue;
            if (m_activeJumpParticleInstance != null &&
                (childObj == m_activeJumpParticleInstance || m_activeJumpParticleInstance.transform.IsChildOf(childObj.transform)))
                continue;

            m_savedChildStates[childObj] = childObj.activeSelf;
            if (childObj.activeSelf)
            {
                childObj.SetActive(false);
            }
        }

        jumpVisualObject.SetActive(true);
        m_isJumpVisualActive = true;
    }

    private void RestoreGroundedChildVisuals()
    {
        if (!m_isJumpVisualActive)
            return;

        foreach (var kvp in m_savedChildStates)
        {
            if (kvp.Key != null)
            {
                kvp.Key.SetActive(kvp.Value);
            }
        }
        m_savedChildStates.Clear();

        if (jumpVisualObject != null)
        {
            m_currentAirAngle = 0f;
            jumpVisualObject.transform.localRotation = m_initialJumpVisualRotation;
            if (hideJumpVisualWhenGrounded)
            {
                jumpVisualObject.SetActive(false);
            }
        }

        m_isJumpVisualActive = false;
    }

    private void InitializeJumpParticleObject()
    {
        if (jumpParticleObject == null)
            return;

        if (jumpParticleObject.scene.IsValid())
        {
            m_activeJumpParticleInstance = jumpParticleObject;
        }
        else if (m_activeJumpParticleInstance == null)
        {
            m_activeJumpParticleInstance = Instantiate(jumpParticleObject, transform);
            m_activeJumpParticleInstance.name = $"{jumpParticleObject.name}_Runtime";
            m_activeJumpParticleInstance.transform.localPosition = new Vector3(jumpParticleLocalOffset.x, jumpParticleLocalOffset.y, 0f);
        }

        if (m_activeJumpParticleInstance != null)
        {
            m_jumpParticleSystems = m_activeJumpParticleInstance.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < m_jumpParticleSystems.Length; i++)
            {
                if (m_jumpParticleSystems[i] != null)
                {
                    m_jumpParticleSystems[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }
        }
    }

    private void PlayJumpParticles()
    {
        if (jumpParticleObject == null)
            return;

        if (m_activeJumpParticleInstance == null || (jumpParticleObject.scene.IsValid() && m_activeJumpParticleInstance != jumpParticleObject))
        {
            InitializeJumpParticleObject();
        }

        if (m_activeJumpParticleInstance == null)
            return;

        if (!m_activeJumpParticleInstance.activeSelf)
            m_activeJumpParticleInstance.SetActive(true);

        if (m_activeJumpParticleInstance.transform.parent == transform && jumpParticleLocalOffset.sqrMagnitude > 0.0001f)
        {
            m_activeJumpParticleInstance.transform.localPosition = new Vector3(jumpParticleLocalOffset.x, jumpParticleLocalOffset.y, 0f);
        }

        if (m_jumpParticleSystems == null || m_jumpParticleSystems.Length == 0)
        {
            m_jumpParticleSystems = m_activeJumpParticleInstance.GetComponentsInChildren<ParticleSystem>(true);
        }

        for (int i = 0; i < m_jumpParticleSystems.Length; i++)
        {
            ParticleSystem ps = m_jumpParticleSystems[i];
            if (ps != null)
            {
                if (!ps.gameObject.activeSelf)
                    ps.gameObject.SetActive(true);
                ps.Play(true);
            }
        }
    }

    private void StopJumpParticles()
    {
        if (m_jumpParticleSystems == null)
            return;

        for (int i = 0; i < m_jumpParticleSystems.Length; i++)
        {
            ParticleSystem ps = m_jumpParticleSystems[i];
            if (ps != null && ps.isPlaying)
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }
    }

    private void UpdateAirRotation(float dt)
    {
        if (!m_isJumpVisualActive || jumpVisualObject == null || playerRigidBody == null || dt <= 0f)
            return;

        Vector2 vel = playerRigidBody.linearVelocity;
        if (Mathf.Abs(vel.x) > 0.1f)
        {
            m_facingSign = vel.x >= 0f ? 1f : -1f;
            if (flipJumpVisualByHorizontalVelocity && jumpSpriteRenderer != null)
            {
                jumpSpriteRenderer.flipX = m_facingSign < 0f;
            }
        }

        float tiltSign = flipJumpVisualByHorizontalVelocity ? m_facingSign : 1f;

        if (airRotationMode == AirRotationMode.ContinuousFlipSpin)
        {
            float targetSpinRate = rotationalSpeed * -tiltSign;
            m_currentAirAngle += targetSpinRate * dt;
            Quaternion targetRot = m_initialJumpVisualRotation * Quaternion.Euler(0f, 0f, m_currentAirAngle);
            float t = 1f - Mathf.Exp(-Mathf.Max(0.1f, airRotationSmoothSpeed) * dt);
            jumpVisualObject.transform.localRotation = Quaternion.Slerp(jumpVisualObject.transform.localRotation, targetRot, t);
        }
        else
        {
            float speedNorm = Mathf.Clamp(vel.y / Mathf.Max(0.5f, referenceVerticalSpeed), -1f, 1f);
            float rawTargetAngle = speedNorm >= 0f
                ? Mathf.Lerp(0f, maxUpwardTiltAngle, speedNorm)
                : Mathf.Lerp(0f, maxDownwardTiltAngle, -speedNorm);

            float targetAngle = rawTargetAngle * tiltSign;
            float maxStep = Mathf.Abs(rotationalSpeed) * dt;
            float limitedTarget = Mathf.MoveTowardsAngle(m_currentAirAngle, targetAngle, maxStep);
            float t = 1f - Mathf.Exp(-Mathf.Max(0.1f, airRotationSmoothSpeed) * dt);
            m_currentAirAngle = Mathf.LerpAngle(m_currentAirAngle, limitedTarget, t);
            jumpVisualObject.transform.localRotation = m_initialJumpVisualRotation * Quaternion.Euler(0f, 0f, m_currentAirAngle);
        }
    }

    private bool CheckIsGrounded()
    {
        if (playerCollider == null)
            TryGetComponent(out playerCollider);

        int layerMask = groundLayer.value != 0 ? groundLayer.value : Physics2D.DefaultRaycastLayers;
        Vector2 checkCenter = GetGroundCheckCenter();

        // 1. Check active physics contacts using the surface normal pointing OUT from the ground
        if (playerCollider != null)
        {
            int contactCount = playerCollider.GetContacts(m_contacts);
            for (int i = 0; i < contactCount; i++)
            {
                Collider2D otherCol = m_contacts[i].collider;
                if (otherCol == null || otherCol.isTrigger)
                    continue;
                if (otherCol == playerCollider || otherCol.transform == transform || otherCol.transform.IsChildOf(transform))
                    continue;
                if ((layerMask & (1 << otherCol.gameObject.layer)) == 0)
                    continue;

                Vector2 surfaceNormal = m_contacts[i].normal;
                if (surfaceNormal.sqrMagnitude <= 0.0001f)
                    surfaceNormal = (checkCenter - m_contacts[i].point);

                surfaceNormal.Normalize();
                if (Vector2.Dot(surfaceNormal, checkCenter - m_contacts[i].point) < 0f)
                {
                    surfaceNormal = -surfaceNormal;
                }

                if (IsGroundNormalValid(surfaceNormal))
                {
                    m_lastGroundNormal = surfaceNormal;
                    m_lastGroundPoint = m_contacts[i].point;
                    m_hasGroundHit = true;
                    return true;
                }
            }
        }

        // 2. Check nearby ground colliders radially around the center (no -Y bias) and compute surface normal from the ground
        float searchRadius = GetRadialSearchRadius();
        Collider2D[] hits = Physics2D.OverlapCircleAll(checkCenter, searchRadius, layerMask);
        float bestDistance = float.MaxValue;
        bool foundValidGround = false;
        Vector2 bestNormal = Vector2.up;
        Vector2 bestPoint = checkCenter;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D col = hits[i];
            if (col == null || col.isTrigger)
                continue;
            if (col == playerCollider || col.transform == transform || col.transform.IsChildOf(transform))
                continue;

            if (playerCollider != null && groundCheck == null && groundCheckLocalOffset.sqrMagnitude <= 0.0001f)
            {
                ColliderDistance2D colDist = Physics2D.Distance(playerCollider, col);
                if (!colDist.isValid || colDist.distance > groundCheckDistance)
                    continue;

                Vector2 groundPt = colDist.pointB;
                Vector2 playerPt = colDist.pointA;
                Vector2 surfaceNormal = (playerPt - groundPt).sqrMagnitude > 0.000001f
                    ? (playerPt - groundPt).normalized
                    : (-colDist.normal).normalized;

                if (Vector2.Dot(surfaceNormal, checkCenter - groundPt) < 0f)
                    surfaceNormal = -surfaceNormal;

                if (!IsGroundNormalValid(surfaceNormal))
                    continue;

                if (colDist.distance < bestDistance)
                {
                    bestDistance = colDist.distance;
                    bestNormal = surfaceNormal;
                    bestPoint = groundPt;
                    foundValidGround = true;
                }
            }
            else
            {
                Vector2 groundPt = col.ClosestPoint(checkCenter);
                Vector2 delta = checkCenter - groundPt;
                float dist = delta.magnitude;
                if (dist > groundCheckRadius)
                    continue;

                Vector2 surfaceNormal = dist > 0.0001f ? (delta / dist) : Vector2.up;
                if (!IsGroundNormalValid(surfaceNormal))
                    continue;

                if (dist < bestDistance)
                {
                    bestDistance = dist;
                    bestNormal = surfaceNormal;
                    bestPoint = groundPt;
                    foundValidGround = true;
                }
            }
        }

        if (foundValidGround)
        {
            m_lastGroundNormal = bestNormal;
            m_lastGroundPoint = bestPoint;
            m_hasGroundHit = true;
            return true;
        }

        m_hasGroundHit = false;
        return false;
    }

    private Vector2 GetGroundCheckCenter()
    {
        Transform refTransform = groundCheck != null ? groundCheck : transform;
        Vector2 baseCenter = groundCheck != null
            ? (Vector2)groundCheck.position
            : (playerCollider != null ? (Vector2)playerCollider.bounds.center : (Vector2)transform.position);

        if (groundCheckLocalOffset.sqrMagnitude > 0.0001f)
        {
            baseCenter += (Vector2)refTransform.TransformVector(groundCheckLocalOffset);
        }

        return baseCenter;
    }

    private float GetRadialSearchRadius()
    {
        if (groundCheck == null && groundCheckLocalOffset.sqrMagnitude <= 0.0001f && playerCollider != null)
            return Mathf.Max(groundCheckRadius, playerCollider.bounds.extents.magnitude + groundCheckDistance);

        return groundCheckRadius;
    }

    private bool IsGroundNormalValid(Vector2 groundNormal)
    {
        if (!filterNormalByGravity)
            return groundNormal.sqrMagnitude > 0.0001f;

        Vector2 gravityUp = Physics2D.gravity.sqrMagnitude > 0.0001f
            ? -Physics2D.gravity.normalized
            : Vector2.up;

        return Vector2.Dot(groundNormal.normalized, gravityUp) >= minGravityNormalDot;
    }

    private void OnDrawGizmos()
    {
        if (!showGizmos) return;
        DrawGroundNormalGizmos();
    }

    private void DrawGroundNormalGizmos()
    {
        if (playerCollider == null)
            TryGetComponent(out playerCollider);

        bool groundedPreview = Application.isPlaying ? isGrounded : CheckIsGrounded();
        Vector2 checkCenter = GetGroundCheckCenter();
        float radius = GetRadialSearchRadius();

        Gizmos.color = groundedPreview ? new Color(0.2f, 1f, 0.45f, 0.85f) : new Color(1f, 0.85f, 0.2f, 0.75f);
        Gizmos.DrawWireSphere(checkCenter, radius);

        if (m_hasGroundHit)
        {
            Vector3 origin = new Vector3(m_lastGroundPoint.x, m_lastGroundPoint.y, transform.position.z);
            Vector2 normalDir = m_lastGroundNormal.sqrMagnitude > 0.0001f ? m_lastGroundNormal.normalized : Vector2.up;
            float len = Mathf.Max(0.2f, normalGizmoLength);
            Vector3 tip = origin + (Vector3)(normalDir * len);

            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(origin, 0.07f);
            Gizmos.DrawLine(origin, tip);

            Vector2 perp = new Vector2(-normalDir.y, normalDir.x);
            const float headLen = 0.2f;
            Gizmos.DrawLine(tip, tip - (Vector3)(normalDir * headLen) + (Vector3)(perp * headLen * 0.55f));
            Gizmos.DrawLine(tip, tip - (Vector3)(normalDir * headLen) - (Vector3)(perp * headLen * 0.55f));
        }
    }
}