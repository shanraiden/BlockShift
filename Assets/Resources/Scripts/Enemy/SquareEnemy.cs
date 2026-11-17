using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Square Enemy: Heavy Shield Blocker & Guardian.
/// Anchors in place to physically block lanes, raises a directional (or full-body)
/// shield that reduces/negates incoming damage, pushes attackers back on block,
/// and optionally executes a short-range Shield Bash or cycles guard openings.
/// </summary>
public class SquareEnemy : Enemy
{
    public enum SquareState
    {
        Idle,
        Guarding,
        GuardBreakOpen,
        ShieldBashing
    }

    [Header("Square Blocking Mechanics")]
    [SerializeField] private bool startBlocking = true;
    [SerializeField] private float guardTriggerRange = 6f;
    [SerializeField] private bool fullBodyBlock = false;
    [SerializeField] [Range(30f, 360f)] private float blockingArcAngle = 160f;
    [SerializeField] [Range(0f, 1f)] private float blockedDamageMultiplier = 0f;
    [SerializeField] private float blockRepelForce = 7.5f;

    [Header("Timed Guard Cycle (Optional Openings)")]
    [SerializeField] private bool useTimedGuardCycle = false;
    [SerializeField] private float activeGuardDuration = 3.0f;
    [SerializeField] private float vulnerableOpenDuration = 1.5f;

    [Header("Physical Anchor & Shield Bash")]
    [SerializeField] private float unblockedMass = 2f;
    [SerializeField] private float blockingMass = 100f;
    [SerializeField] private bool lockHorizontalMotionWhileBlocking = true;
    [SerializeField] private bool enableShieldBash = true;
    [SerializeField] private float bashTriggerRange = 1.6f;
    [SerializeField] private float bashForce = 9.5f;
    [SerializeField] private float bashCooldown = 2.0f;

    [Header("Visual Feedback")]
    [SerializeField] private GameObject shieldVisualObject;
    [SerializeField] private Color guardingColor = new Color(0.35f, 0.75f, 1f, 1f);
    [SerializeField] private Color vulnerableColor = new Color(0.8f, 0.8f, 0.8f, 1f);

    [Header("Block Events")]
    public UnityEvent onAttackBlocked;
    public UnityEvent onShieldBash;

    [Header("Status (Read-Only)")]
    [SerializeField] private SquareState currentState = SquareState.Guarding;
    [SerializeField] private bool isBlocking;

    private float m_cycleTimer;
    private float m_nextBashTime;
    private float m_bashVisualTimer;

    public bool IsBlocking => isBlocking;

    public override void SetupDefaultEnemyObjects()
    {
        base.SetupDefaultEnemyObjects();

        SpriteRenderer bodySr = GetComponent<SpriteRenderer>();
        if (bodySr == null)
        {
            bodySr = gameObject.AddComponent<SpriteRenderer>();
        }
        EnsureSpriteAssigned(bodySr, EnemyShapeKind.Square, guardingColor);
        spriteRenderer = bodySr;

        if (shieldVisualObject == null)
        {
            Transform existingShield = transform.Find("ShieldVisual");
            if (existingShield != null)
            {
                shieldVisualObject = existingShield.gameObject;
            }
            else
            {
                GameObject shieldObj = new GameObject("ShieldVisual");
                shieldObj.transform.SetParent(transform, false);
                shieldObj.transform.localPosition = new Vector3(0.6f, 0f, 0f);
                shieldVisualObject = shieldObj;
            }
        }

        SpriteRenderer shieldSr = shieldVisualObject.GetComponent<SpriteRenderer>();
        if (shieldSr == null)
        {
            shieldSr = shieldVisualObject.AddComponent<SpriteRenderer>();
        }
        EnsureSpriteAssigned(shieldSr, EnemyShapeKind.ShieldBar, Color.cyan);
        shieldSr.sortingOrder = bodySr.sortingOrder + 1;
    }

    protected override void OnEnemyAwake()
    {
        SetupDefaultEnemyObjects();

        if (rb != null)
        {
            rb.freezeRotation = true;
        }
        SetBlockingState(startBlocking);
        m_cycleTimer = activeGuardDuration;
    }

    protected override void UpdateEnemyBehavior(float dt)
    {
        if (m_bashVisualTimer > 0f)
        {
            m_bashVisualTimer = Mathf.Max(0f, m_bashVisualTimer - dt);
            float t = m_bashVisualTimer / 0.18f;
            transform.localScale = new Vector3(baseScale.x * (1f + 0.22f * t), baseScale.y * (1f - 0.1f * t), baseScale.z);
            if (m_bashVisualTimer <= 0f)
            {
                transform.localScale = baseScale;
            }
        }

        if (currentTarget != null)
        {
            Vector2 toTarget = (Vector2)currentTarget.position - (Vector2)transform.position;
            float dist = toTarget.magnitude;

            if (!isBlocking || !lockHorizontalMotionWhileBlocking)
            {
                SetFacingDirection(new Vector2(toTarget.x >= 0f ? 1f : -1f, 0f));
            }
            else if (Mathf.Abs(toTarget.x) > 0.1f)
            {
                // Turn slowly/cleanly toward the player while guarding
                SetFacingDirection(new Vector2(toTarget.x >= 0f ? 1f : -1f, 0f));
            }

            if (useTimedGuardCycle)
            {
                m_cycleTimer -= dt;
                if (m_cycleTimer <= 0f)
                {
                    bool nextBlock = !isBlocking;
                    SetBlockingState(nextBlock);
                    m_cycleTimer = nextBlock ? Mathf.Max(0.3f, activeGuardDuration) : Mathf.Max(0.3f, vulnerableOpenDuration);
                }
            }
            else
            {
                SetBlockingState(dist <= guardTriggerRange);
            }

            if (enableShieldBash && isBlocking && dist <= bashTriggerRange && Time.time >= m_nextBashTime)
            {
                PerformShieldBash();
            }
        }
        else
        {
            SetBlockingState(startBlocking);
            currentState = startBlocking ? SquareState.Guarding : SquareState.Idle;
        }
    }

    protected override void FixedUpdateEnemyBehavior(float fixedDt)
    {
        if (rb == null || rb.bodyType != RigidbodyType2D.Dynamic)
            return;

        if (isBlocking && lockHorizontalMotionWhileBlocking)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        }
    }

    public override bool TakeDamage(float amount, Vector2 hitImpulse = default, Vector2 hitSourceWorldPos = default)
    {
        if (isDead || amount <= 0f)
            return false;

        if (IsHitBlocked(hitImpulse, hitSourceWorldPos))
        {
            onAttackBlocked?.Invoke();
            m_bashVisualTimer = 0.14f;

            // Push attacker / impulse source away when blocked
            if (currentTarget != null && blockRepelForce > 0f &&
                Vector2.Distance(transform.position, currentTarget.position) <= bashTriggerRange * 1.6f)
            {
                ApplyDamageAndKnockbackToTarget(currentTarget.gameObject, 0f, blockRepelForce, transform.position);
            }

            float reducedDamage = amount * blockedDamageMultiplier;
            if (reducedDamage <= 0.001f)
            {
                return false;
            }

            return base.TakeDamage(reducedDamage, Vector2.zero, hitSourceWorldPos);
        }

        return base.TakeDamage(amount, hitImpulse, hitSourceWorldPos);
    }

    private bool IsHitBlocked(Vector2 hitImpulse, Vector2 hitSourceWorldPos)
    {
        if (!isBlocking)
            return false;

        if (fullBodyBlock)
            return true;

        Vector2 dirToAttacker = Vector2.zero;
        if (hitSourceWorldPos != default && (hitSourceWorldPos - (Vector2)transform.position).sqrMagnitude > 0.0001f)
        {
            dirToAttacker = (hitSourceWorldPos - (Vector2)transform.position).normalized;
        }
        else if (hitImpulse.sqrMagnitude > 0.0001f)
        {
            dirToAttacker = (-hitImpulse).normalized;
        }
        else
        {
            return true;
        }

        float angle = Vector2.Angle(facingDirection, dirToAttacker);
        return angle <= blockingArcAngle * 0.5f;
    }

    private void PerformShieldBash()
    {
        if (currentTarget == null) return;

        currentState = SquareState.ShieldBashing;
        m_nextBashTime = Time.time + Mathf.Max(0.3f, bashCooldown);
        m_bashVisualTimer = 0.18f;

        ApplyDamageAndKnockbackToTarget(currentTarget.gameObject, contactDamage, bashForce, transform.position);
        onShieldBash?.Invoke();
    }

    private void SetBlockingState(bool blocking)
    {
        isBlocking = blocking;
        currentState = blocking ? SquareState.Guarding : (useTimedGuardCycle ? SquareState.GuardBreakOpen : SquareState.Idle);

        if (rb != null)
        {
            rb.mass = blocking ? Mathf.Max(1f, blockingMass) : Mathf.Max(0.1f, unblockedMass);
        }

        if (shieldVisualObject != null)
        {
            shieldVisualObject.SetActive(blocking);
            Vector3 lp = shieldVisualObject.transform.localPosition;
            float sign = facingDirection.x < -0.05f ? -1f : 1f;
            shieldVisualObject.transform.localPosition = new Vector3(Mathf.Abs(lp.x) * sign, lp.y, lp.z);
        }

        if (spriteRenderer != null)
        {
            spriteRenderer.color = blocking ? guardingColor : vulnerableColor;
        }
    }

    protected override void OnCollisionEnter2D(Collision2D collision)
    {
        base.OnCollisionEnter2D(collision);

        // Repel projectiles or rushing attackers physically on contact while blocking
        if (isBlocking && collision != null && collision.collider != null && MatchesTargetTag(collision.collider.gameObject))
        {
            if (blockRepelForce > 0f)
            {
                ApplyDamageAndKnockbackToTarget(collision.collider.gameObject, 0f, blockRepelForce * 0.65f, transform.position);
            }
        }
    }

    public override void ResetEnemy()
    {
        base.ResetEnemy();
        SetBlockingState(startBlocking);
        m_cycleTimer = activeGuardDuration;
        m_nextBashTime = 0f;
    }

    protected override void DrawEnemyGizmos()
    {
        base.DrawEnemyGizmos();

        Vector3 pos = transform.position;
        Gizmos.color = new Color(0.25f, 0.7f, 1f, 0.75f);
        DrawWireCircle2D(pos, guardTriggerRange, 32);

        if (enableShieldBash)
        {
            Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.8f);
            DrawWireCircle2D(pos, bashTriggerRange, 24);
        }

        // Draw blocking shield arc in front of SquareEnemy
        if (!fullBodyBlock)
        {
            Gizmos.color = Color.cyan;
            Vector2 dir = facingDirection.sqrMagnitude > 0.001f ? facingDirection.normalized : Vector2.right;
            float halfAngle = blockingArcAngle * 0.5f * Mathf.Deg2Rad;
            float baseAngle = Mathf.Atan2(dir.y, dir.x);
            Vector3 leftRay = new Vector3(Mathf.Cos(baseAngle + halfAngle), Mathf.Sin(baseAngle + halfAngle), 0f) * 1.1f;
            Vector3 rightRay = new Vector3(Mathf.Cos(baseAngle - halfAngle), Mathf.Sin(baseAngle - halfAngle), 0f) * 1.1f;
            Gizmos.DrawLine(pos, pos + leftRay);
            Gizmos.DrawLine(pos, pos + rightRay);
        }
    }
}

