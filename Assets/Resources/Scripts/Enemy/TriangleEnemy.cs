using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Triangle Enemy: Ranged Projectile Thrower.
/// Aims at detected targets (with optional velocity prediction), telegraphs before firing,
/// supports SingleShot, TripleSpread, and LobbedArc throw patterns, and optionally kites away
/// if the player gets too close.
/// </summary>
public class TriangleEnemy : Enemy
{
    public enum ThrowPattern
    {
        SingleShot,
        TripleSpread,
        LobbedArc
    }

    public enum TriangleState
    {
        Idle,
        Aiming,
        Windup,
        Cooldown
    }

    [Header("Triangle Projectile Throwing")]
    [SerializeField] private ThrowPattern throwPattern = ThrowPattern.SingleShot;
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform fireOrigin;
    [SerializeField] private float attackRange = 8f;
    [SerializeField] private float throwCooldown = 1.6f;
    [SerializeField] private float windupDuration = 0.35f;

    [Header("Projectile Ballistics")]
    [SerializeField] private float projectileSpeed = 9f;
    [SerializeField] private float projectileDamage = 1f;
    [SerializeField] private float projectileKnockback = 5f;
    [SerializeField] private float projectileLifetime = 4f;
    [SerializeField] private float projectileGravityScale = 0f;
    [SerializeField] private bool leadMovingTargets = true;

    [Header("Spread / Lob Pattern Settings")]
    [SerializeField] [Range(5f, 60f)] private float spreadAngleDegrees = 18f;
    [SerializeField] private float lobArcGravityScale = 1.5f;
    [SerializeField] private float lobExtraUpwardLift = 4.5f;

    [Header("Kiting / Positioning (Optional)")]
    [SerializeField] private bool isStationaryTurret = false;
    [SerializeField] private float retreatDistance = 2.8f;
    [SerializeField] private float retreatSpeed = 2.8f;

    [Header("Telegraph Visuals")]
    [SerializeField] private Color windupTintColor = new Color(1f, 0.85f, 0.2f, 1f);
    [SerializeField] private Color runtimeProjectileColor = new Color(1f, 0.35f, 0.2f, 1f);

    [Header("Events")]
    public UnityEvent onProjectileThrown;

    [Header("Status (Read-Only)")]
    [SerializeField] private TriangleState currentState = TriangleState.Idle;

    private float m_stateTimer;
    private float m_nextThrowAllowedTime;
    private Vector2 m_aimDirection = Vector2.right;
    private Vector2 m_moveVelocity;
    private Color m_cachedBaseColor = Color.white;
    private Vector3 m_cachedBaseScale = Vector3.one;

    public override void SetupDefaultEnemyObjects()
    {
        base.SetupDefaultEnemyObjects();

        SpriteRenderer bodySr = GetComponent<SpriteRenderer>();
        if (bodySr == null)
        {
            bodySr = gameObject.AddComponent<SpriteRenderer>();
        }
        EnsureSpriteAssigned(bodySr, EnemyShapeKind.Triangle, new Color(1f, 0.35f, 0.35f, 1f));
        spriteRenderer = bodySr;

        if (fireOrigin == null)
        {
            Transform existingOrigin = transform.Find("FireOrigin");
            if (existingOrigin != null)
            {
                fireOrigin = existingOrigin;
            }
            else
            {
                GameObject originObj = new GameObject("FireOrigin");
                originObj.transform.SetParent(transform, false);
                originObj.transform.localPosition = new Vector3(0.65f, 0f, 0f);
                fireOrigin = originObj.transform;
            }
        }
    }

    protected override void OnEnemyAwake()
    {
        SetupDefaultEnemyObjects();

        m_cachedBaseScale = transform.localScale;
        if (spriteRenderer != null)
        {
            m_cachedBaseColor = spriteRenderer.color;
        }
        if (rb != null)
        {
            rb.freezeRotation = true;
        }
    }

    protected override void UpdateEnemyBehavior(float dt)
    {
        if (currentTarget == null)
        {
            currentState = TriangleState.Idle;
            m_moveVelocity = Vector2.zero;
            RestoreVisuals();
            return;
        }

        Vector3 origin = GetFirePosition();
        Vector2 toTarget = (Vector2)currentTarget.position - (Vector2)origin;
        float dist = toTarget.magnitude;

        UpdateAimDirection(origin, toTarget);
        UpdateKitingMovement(toTarget, dist);

        switch (currentState)
        {
            case TriangleState.Idle:
            case TriangleState.Aiming:
                currentState = TriangleState.Aiming;
                if (dist <= attackRange && Time.time >= m_nextThrowAllowedTime)
                {
                    BeginWindup();
                }
                break;

            case TriangleState.Windup:
                m_stateTimer -= dt;
                float pulse = 1f + Mathf.Sin(Time.time * 24f) * 0.1f;
                transform.localScale = new Vector3(m_cachedBaseScale.x * 0.9f, m_cachedBaseScale.y * (1.15f * pulse), m_cachedBaseScale.z);

                if (m_stateTimer <= 0f)
                {
                    FireProjectiles();
                    RestoreVisuals();
                    currentState = TriangleState.Cooldown;
                    m_stateTimer = 0.15f;
                    m_nextThrowAllowedTime = Time.time + Mathf.Max(0.25f, throwCooldown);
                }
                break;

            case TriangleState.Cooldown:
                m_stateTimer -= dt;
                if (m_stateTimer <= 0f)
                {
                    currentState = TriangleState.Aiming;
                }
                break;
        }
    }

    protected override void FixedUpdateEnemyBehavior(float fixedDt)
    {
        if (rb == null || rb.bodyType != RigidbodyType2D.Dynamic)
            return;

        if (isStationaryTurret)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            return;
        }

        rb.linearVelocity = new Vector2(m_moveVelocity.x, rb.linearVelocity.y);
    }

    private void UpdateAimDirection(Vector3 origin, Vector2 rawToTarget)
    {
        Vector2 aimTargetPos = currentTarget.position;

        if (leadMovingTargets && projectileSpeed > 0.1f)
        {
            Rigidbody2D targetRb = currentTarget.GetComponentInParent<Rigidbody2D>();
            if (targetRb != null)
            {
                float flightTime = rawToTarget.magnitude / projectileSpeed;
                aimTargetPos += targetRb.linearVelocity * (flightTime * 0.75f);
            }
        }

        Vector2 dir = aimTargetPos - (Vector2)origin;
        if (dir.sqrMagnitude > 0.0001f)
        {
            m_aimDirection = dir.normalized;
            SetFacingDirection(m_aimDirection);
        }
    }

    private void UpdateKitingMovement(Vector2 toTarget, float dist)
    {
        if (isStationaryTurret || currentState == TriangleState.Windup)
        {
            m_moveVelocity = Vector2.zero;
            return;
        }

        if (dist < retreatDistance)
        {
            float awaySign = toTarget.x >= 0f ? -1f : 1f;
            m_moveVelocity = new Vector2(awaySign * retreatSpeed, 0f);
        }
        else
        {
            m_moveVelocity = Vector2.zero;
        }
    }

    private void BeginWindup()
    {
        currentState = TriangleState.Windup;
        m_stateTimer = Mathf.Max(0.05f, windupDuration);
        if (spriteRenderer != null)
        {
            spriteRenderer.color = windupTintColor;
        }
    }

    private void FireProjectiles()
    {
        Vector3 spawnPos = GetFirePosition();

        switch (throwPattern)
        {
            case ThrowPattern.SingleShot:
                SpawnProjectile(spawnPos, m_aimDirection * projectileSpeed, projectileGravityScale);
                break;

            case ThrowPattern.TripleSpread:
                SpawnProjectile(spawnPos, m_aimDirection * projectileSpeed, projectileGravityScale);
                SpawnProjectile(spawnPos, RotateVector2(m_aimDirection, spreadAngleDegrees) * projectileSpeed, projectileGravityScale);
                SpawnProjectile(spawnPos, RotateVector2(m_aimDirection, -spreadAngleDegrees) * projectileSpeed, projectileGravityScale);
                break;

            case ThrowPattern.LobbedArc:
                Vector2 lobVelocity = ComputeLobVelocity(spawnPos, currentTarget != null ? (Vector2)currentTarget.position : (Vector2)spawnPos + m_aimDirection * 4f);
                SpawnProjectile(spawnPos, lobVelocity, Mathf.Max(0.2f, lobArcGravityScale));
                break;
        }

        onProjectileThrown?.Invoke();
    }

    private Vector2 ComputeLobVelocity(Vector2 origin, Vector2 targetPos)
    {
        float dx = targetPos.x - origin.x;
        float dy = targetPos.y - origin.y;
        float dist = Mathf.Max(0.5f, Mathf.Abs(dx));
        float flightTime = Mathf.Clamp(dist / Mathf.Max(2f, projectileSpeed), 0.35f, 1.5f);

        float gravity = Mathf.Abs(Physics2D.gravity.y) * Mathf.Max(0.2f, lobArcGravityScale);
        float vx = dx / flightTime;
        float vy = (dy + 0.5f * gravity * flightTime * flightTime) / flightTime;
        vy = Mathf.Max(vy, lobExtraUpwardLift);

        return new Vector2(vx, vy);
    }

    private void SpawnProjectile(Vector3 spawnPos, Vector2 velocity, float gravScale)
    {
        GameObject projObj;
        if (projectilePrefab != null)
        {
            projObj = Instantiate(projectilePrefab, spawnPos, Quaternion.identity);
        }
        else
        {
            projObj = CreateFallbackProjectile(spawnPos);
        }

        EnemyProjectile2D proj = projObj.GetComponent<EnemyProjectile2D>();
        if (proj == null)
        {
            proj = projObj.AddComponent<EnemyProjectile2D>();
        }

        proj.Initialize(
            gameObject,
            velocity,
            projectileDamage,
            projectileKnockback,
            gravScale,
            projectileLifetime,
            targetTag
        );
    }

    private GameObject CreateFallbackProjectile(Vector3 spawnPos)
    {
        GameObject obj = new GameObject($"{name}_Projectile");
        obj.transform.position = spawnPos;

        Rigidbody2D projRb = obj.AddComponent<Rigidbody2D>();
        projRb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        CircleCollider2D col = obj.AddComponent<CircleCollider2D>();
        col.radius = 0.16f;
        col.isTrigger = true;

        SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite = CreateTriangleProjectileSprite();
        sr.color = runtimeProjectileColor;
        sr.sortingOrder = spriteRenderer != null ? spriteRenderer.sortingOrder + 1 : 5;

        return obj;
    }

    private static Sprite s_cachedFallbackSprite;
    private static Sprite CreateTriangleProjectileSprite()
    {
        if (s_cachedFallbackSprite != null)
            return s_cachedFallbackSprite;

        const int size = 16;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        Color clear = new Color(0f, 0f, 0f, 0f);

        for (int y = 0; y < size; y++)
        {
            float t = (float)y / (size - 1);
            int halfSpan = Mathf.RoundToInt((1f - t) * (size * 0.45f));
            int centerX = size / 2;
            for (int x = 0; x < size; x++)
            {
                bool inside = Mathf.Abs(x - centerX) <= halfSpan;
                tex.SetPixel(x, y, inside ? Color.white : clear);
            }
        }
        tex.Apply();
        s_cachedFallbackSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 48f);
        return s_cachedFallbackSprite;
    }

    private Vector3 GetFirePosition()
    {
        return fireOrigin != null ? fireOrigin.position : transform.position + (Vector3)(m_aimDirection * 0.6f);
    }

    private static Vector2 RotateVector2(Vector2 v, float degrees)
    {
        float rad = degrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad);
        float sin = Mathf.Sin(rad);
        return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
    }

    private void RestoreVisuals()
    {
        transform.localScale = m_cachedBaseScale;
        if (spriteRenderer != null)
        {
            spriteRenderer.color = m_cachedBaseColor;
        }
    }

    public override void ResetEnemy()
    {
        base.ResetEnemy();
        currentState = TriangleState.Idle;
        m_nextThrowAllowedTime = 0f;
        RestoreVisuals();
    }

    protected override void DrawEnemyGizmos()
    {
        base.DrawEnemyGizmos();

        Vector3 origin = GetFirePosition();
        Gizmos.color = new Color(1f, 0.25f, 0.25f, 0.7f);
        DrawWireCircle2D(transform.position, attackRange, 36);

        if (!isStationaryTurret && retreatDistance > 0f)
        {
            Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.5f);
            DrawWireCircle2D(transform.position, retreatDistance, 24);
        }

        Gizmos.color = Color.red;
        Gizmos.DrawLine(origin, origin + (Vector3)(m_aimDirection * 1.4f));
    }
}

