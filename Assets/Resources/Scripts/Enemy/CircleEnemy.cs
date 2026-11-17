using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Circle Enemy: Mobile roller/charger.
/// Patrols ground platforms or waypoints, chases detected targets at increased speed,
/// rolls visually according to velocity, and performs telegraphed dash lunges.
/// </summary>
public class CircleEnemy : Enemy
{
    public enum MovementMode
    {
        GroundPatrol,
        WaypointPatrol,
        FreeChase2D
    }

    public enum CircleState
    {
        Patrol,
        Chase,
        LungeWindup,
        Lunging,
        Cooldown
    }

    [Header("Circle Movement Settings")]
    [SerializeField] private MovementMode movementMode = MovementMode.GroundPatrol;
    [SerializeField] private float patrolSpeed = 2.5f;
    [SerializeField] private float chaseSpeed = 4.5f;
    [SerializeField] private float acceleration = 12f;
    [SerializeField] private float patrolMaxDistance = 4f;
    [SerializeField] private bool startFacingRight = true;

    [Header("Waypoint Patrol (Optional)")]
    [SerializeField] private List<Transform> waypoints = new List<Transform>();
    [SerializeField] private float waypointReachThreshold = 0.25f;

    [Header("Edge & Wall Sensing (GroundPatrol)")]
    [SerializeField] private bool avoidLedgesOnPatrol = true;
    [SerializeField] private LayerMask groundLayerMask = ~0;
    [SerializeField] private float wallCheckDistance = 0.65f;
    [SerializeField] private float ledgeCheckForwardOffset = 0.55f;
    [SerializeField] private float ledgeCheckDepth = 1.1f;

    [Header("Rolling Lunge Attack")]
    [SerializeField] private bool enableLungeAttack = true;
    [SerializeField] private float lungeTriggerRange = 4.0f;
    [SerializeField] private float lungeWindupDuration = 0.35f;
    [SerializeField] private float lungeSpeed = 9.5f;
    [SerializeField] private float lungeDuration = 0.32f;
    [SerializeField] private float lungeCooldown = 1.6f;

    [Header("Rolling Visuals")]
    [SerializeField] private Transform visualSpinTransform;
    [SerializeField] private float spinSpeedDegreesPerUnit = 160f;
    [SerializeField] private Color lungeTelegraphColor = new Color(1f, 0.55f, 0.15f, 1f);

    [Header("Status (Read-Only)")]
    [SerializeField] private CircleState currentState = CircleState.Patrol;

    private int m_patrolSign = 1;
    private int m_waypointIndex;
    private float m_stateTimer;
    private float m_nextLungeAllowedTime;
    private Vector2 m_lungeDirection;
    private Vector2 m_desiredVelocity;
    private Color m_cachedBaseColor = Color.white;
    private Vector3 m_cachedVisualScale = Vector3.one;

    public override void SetupDefaultEnemyObjects()
    {
        base.SetupDefaultEnemyObjects();

        if (visualSpinTransform == null)
        {
            Transform existing = transform.Find("CircleVisual");
            if (existing != null)
            {
                visualSpinTransform = existing;
            }
            else
            {
                GameObject visualObj = new GameObject("CircleVisual");
                visualObj.transform.SetParent(transform, false);
                visualObj.transform.localPosition = Vector3.zero;
                visualSpinTransform = visualObj.transform;
            }
        }

        SpriteRenderer sr = visualSpinTransform.GetComponent<SpriteRenderer>();
        if (sr == null)
        {
            sr = visualSpinTransform.gameObject.AddComponent<SpriteRenderer>();
        }
        EnsureSpriteAssigned(sr, EnemyShapeKind.Circle, new Color(0.25f, 0.85f, 1f, 1f));
        spriteRenderer = sr;
    }

    protected override void OnEnemyAwake()
    {
        SetupDefaultEnemyObjects();

        m_patrolSign = startFacingRight ? 1 : -1;
        facingDirection = m_patrolSign > 0 ? Vector2.right : Vector2.left;

        if (visualSpinTransform == null && spriteRenderer != null)
        {
            visualSpinTransform = spriteRenderer.transform;
        }
        if (visualSpinTransform != null)
        {
            m_cachedVisualScale = visualSpinTransform.localScale;
        }
        if (spriteRenderer != null)
        {
            m_cachedBaseColor = spriteRenderer.color;
        }

        if (movementMode == MovementMode.FreeChase2D && rb != null)
        {
            rb.gravityScale = 0f;
        }
        if (rb != null)
        {
            rb.freezeRotation = true;
        }
    }

    protected override void UpdateEnemyBehavior(float dt)
    {
        switch (currentState)
        {
            case CircleState.Patrol:
                UpdatePatrol(dt);
                if (currentTarget != null)
                {
                    currentState = CircleState.Chase;
                }
                break;

            case CircleState.Chase:
                if (currentTarget == null)
                {
                    currentState = CircleState.Patrol;
                    break;
                }

                UpdateChase(dt);

                if (enableLungeAttack && Time.time >= m_nextLungeAllowedTime)
                {
                    float distToTarget = Vector2.Distance(transform.position, currentTarget.position);
                    if (distToTarget <= lungeTriggerRange)
                    {
                        BeginLungeWindup();
                    }
                }
                break;

            case CircleState.LungeWindup:
                m_stateTimer -= dt;
                m_desiredVelocity = Vector2.zero;

                if (currentTarget != null)
                {
                    Vector2 aimDir = ((Vector2)currentTarget.position - (Vector2)transform.position);
                    if (movementMode != MovementMode.FreeChase2D)
                        aimDir.y = 0f;
                    if (aimDir.sqrMagnitude > 0.001f)
                    {
                        m_lungeDirection = aimDir.normalized;
                        SetFacingDirection(m_lungeDirection);
                    }
                }

                if (visualSpinTransform != null)
                {
                    float pulse = 1f + Mathf.Sin(Time.time * 28f) * 0.08f;
                    visualSpinTransform.localScale = new Vector3(m_cachedVisualScale.x * (1.15f * pulse), m_cachedVisualScale.y * 0.85f, m_cachedVisualScale.z);
                }

                if (m_stateTimer <= 0f)
                {
                    BeginLunge();
                }
                break;

            case CircleState.Lunging:
                m_stateTimer -= dt;
                m_desiredVelocity = m_lungeDirection * lungeSpeed;

                if (m_stateTimer <= 0f)
                {
                    EndLunge();
                }
                break;

            case CircleState.Cooldown:
                m_stateTimer -= dt;
                m_desiredVelocity = Vector2.zero;
                if (m_stateTimer <= 0f)
                {
                    currentState = currentTarget != null ? CircleState.Chase : CircleState.Patrol;
                }
                break;
        }

        UpdateRollingVisual(dt);
    }

    protected override void FixedUpdateEnemyBehavior(float fixedDt)
    {
        if (rb == null || rb.bodyType != RigidbodyType2D.Dynamic)
            return;

        if (movementMode == MovementMode.FreeChase2D)
        {
            rb.linearVelocity = Vector2.MoveTowards(rb.linearVelocity, m_desiredVelocity, acceleration * fixedDt);
        }
        else
        {
            float targetVx = currentState == CircleState.Lunging
                ? m_desiredVelocity.x
                : Mathf.MoveTowards(rb.linearVelocity.x, m_desiredVelocity.x, acceleration * fixedDt);

            rb.linearVelocity = new Vector2(targetVx, rb.linearVelocity.y);
        }
    }

    private void UpdatePatrol(float dt)
    {
        if (movementMode == MovementMode.WaypointPatrol && waypoints != null && waypoints.Count > 0)
        {
            Transform wp = waypoints[m_waypointIndex % waypoints.Count];
            if (wp != null)
            {
                Vector2 toWp = (Vector2)wp.position - (Vector2)transform.position;
                if (toWp.magnitude <= waypointReachThreshold)
                {
                    m_waypointIndex = (m_waypointIndex + 1) % waypoints.Count;
                }
                else
                {
                    Vector2 dir = toWp.normalized;
                    SetFacingDirection(dir);
                    m_desiredVelocity = new Vector2(dir.x * patrolSpeed, 0f);
                }
            }
            return;
        }

        if (movementMode == MovementMode.FreeChase2D)
        {
            float offsetFromHome = transform.position.x - homePosition.x;
            if (Mathf.Abs(offsetFromHome) >= patrolMaxDistance)
            {
                m_patrolSign = offsetFromHome > 0f ? -1 : 1;
            }
            if (HasWallAhead(m_patrolSign))
            {
                m_patrolSign = -m_patrolSign;
            }

            Vector2 dir = new Vector2(m_patrolSign, 0f);
            SetFacingDirection(dir);
            m_desiredVelocity = dir * patrolSpeed;
            return;
        }

        // GroundPatrol
        float distFromHome = transform.position.x - homePosition.x;
        if (Mathf.Abs(distFromHome) >= patrolMaxDistance && Mathf.Sign(distFromHome) == m_patrolSign)
        {
            m_patrolSign = -m_patrolSign;
        }
        else if (HasWallAhead(m_patrolSign) || (avoidLedgesOnPatrol && !HasGroundAhead(m_patrolSign)))
        {
            m_patrolSign = -m_patrolSign;
        }

        SetFacingDirection(new Vector2(m_patrolSign, 0f));
        m_desiredVelocity = new Vector2(m_patrolSign * patrolSpeed, 0f);
    }

    private void UpdateChase(float dt)
    {
        Vector2 toTarget = (Vector2)currentTarget.position - (Vector2)transform.position;
        if (movementMode == MovementMode.FreeChase2D)
        {
            Vector2 dir = toTarget.sqrMagnitude > 0.001f ? toTarget.normalized : Vector2.right;
            SetFacingDirection(dir);
            m_desiredVelocity = dir * chaseSpeed;
            return;
        }

        int moveSign = toTarget.x >= 0f ? 1 : -1;
        SetFacingDirection(new Vector2(moveSign, 0f));

        if (avoidLedgesOnPatrol && !HasGroundAhead(moveSign))
        {
            m_desiredVelocity = Vector2.zero;
        }
        else
        {
            m_desiredVelocity = new Vector2(moveSign * chaseSpeed, 0f);
        }
    }

    private void BeginLungeWindup()
    {
        currentState = CircleState.LungeWindup;
        m_stateTimer = Mathf.Max(0.05f, lungeWindupDuration);
        m_lungeDirection = facingDirection.sqrMagnitude > 0.001f ? facingDirection.normalized : Vector2.right;

        if (spriteRenderer != null)
        {
            spriteRenderer.color = lungeTelegraphColor;
        }
    }

    private void BeginLunge()
    {
        currentState = CircleState.Lunging;
        m_stateTimer = Mathf.Max(0.05f, lungeDuration);
        m_nextLungeAllowedTime = Time.time + lungeDuration + Mathf.Max(0.2f, lungeCooldown);

        if (visualSpinTransform != null)
        {
            visualSpinTransform.localScale = m_cachedVisualScale;
        }
        if (spriteRenderer != null)
        {
            spriteRenderer.color = m_cachedBaseColor;
        }
    }

    private void EndLunge()
    {
        currentState = CircleState.Cooldown;
        m_stateTimer = 0.25f;
        if (visualSpinTransform != null)
        {
            visualSpinTransform.localScale = m_cachedVisualScale;
        }
    }

    private void UpdateRollingVisual(float dt)
    {
        if (visualSpinTransform == null || rb == null)
            return;

        if (visualSpinTransform == transform)
            return; // Avoid rotating physics root unless separate child visual exists

        float horizontalVel = rb.linearVelocity.x;
        if (Mathf.Abs(horizontalVel) > 0.05f)
        {
            visualSpinTransform.Rotate(0f, 0f, -horizontalVel * spinSpeedDegreesPerUnit * dt);
        }
    }

    private bool HasWallAhead(int dirSign)
    {
        Vector2 origin = transform.position;
        Vector2 dir = new Vector2(dirSign, 0f);
        RaycastHit2D[] hits = Physics2D.RaycastAll(origin, dir, wallCheckDistance, groundLayerMask);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D col = hits[i].collider;
            if (col == null || col.isTrigger) continue;
            if (col.transform == transform || col.transform.IsChildOf(transform)) continue;
            if (MatchesTargetTag(col.gameObject)) continue;
            return true;
        }
        return false;
    }

    private bool HasGroundAhead(int dirSign)
    {
        Vector2 origin = (Vector2)transform.position + new Vector2(dirSign * ledgeCheckForwardOffset, 0f);
        RaycastHit2D[] hits = Physics2D.RaycastAll(origin, Vector2.down, ledgeCheckDepth, groundLayerMask);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D col = hits[i].collider;
            if (col == null || col.isTrigger) continue;
            if (col.transform == transform || col.transform.IsChildOf(transform)) continue;
            return true;
        }
        return false;
    }

    public override void ResetEnemy()
    {
        base.ResetEnemy();
        currentState = CircleState.Patrol;
        m_patrolSign = startFacingRight ? 1 : -1;
        m_waypointIndex = 0;
        m_nextLungeAllowedTime = 0f;
        if (visualSpinTransform != null)
        {
            visualSpinTransform.localScale = m_cachedVisualScale;
        }
    }

    protected override void DrawEnemyGizmos()
    {
        base.DrawEnemyGizmos();

        Vector3 pos = transform.position;
        Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.7f);
        Gizmos.DrawLine(new Vector3(homePosition.x - patrolMaxDistance, pos.y, 0f), new Vector3(homePosition.x + patrolMaxDistance, pos.y, 0f));

        if (enableLungeAttack)
        {
            Gizmos.color = new Color(1f, 0.45f, 0.1f, 0.65f);
            DrawWireCircle2D(pos, lungeTriggerRange, 28);
        }
    }
}

