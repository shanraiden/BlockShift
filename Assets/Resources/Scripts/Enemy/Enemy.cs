using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Base class for all 2D Enemies (Circle, Square, Triangle).
/// Provides target detection, line-of-sight checks, health/damage,
/// contact knockback, hit flash feedback, and common Inspector settings.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public abstract class Enemy : MonoBehaviour
{
    public enum EnemyShapeKind { Circle, Square, Triangle, ShieldBar }

    private static readonly Dictionary<EnemyShapeKind, Sprite> s_cachedShapeSprites = new Dictionary<EnemyShapeKind, Sprite>();
    [Header("Identity & Health")]
    [SerializeField] protected float maxHealth = 3f;
    [SerializeField] protected float currentHealth = 3f;
    [SerializeField] [Range(0f, 1f)] protected float knockbackResistance = 0f;
    [SerializeField] protected float hitInvulnerabilityDuration = 0.15f;
    [SerializeField] protected bool destroyOnDeath = true;
    [SerializeField] protected float deathDestroyDelay = 0.05f;

    [Header("Target Detection")]
    [SerializeField] protected string targetTag = "Player";
    [SerializeField] protected float detectionRange = 8f;
    [SerializeField] protected float loseTargetRange = 11f;
    [SerializeField] protected bool requireLineOfSight = false;
    [SerializeField] protected LayerMask lineOfSightBlockers = ~0;

    [Header("Contact Damage & Knockback")]
    [SerializeField] protected bool dealContactDamage = true;
    [SerializeField] protected float contactDamage = 1f;
    [SerializeField] protected float contactKnockbackForce = 6f;
    [SerializeField] protected float contactDamageCooldown = 0.6f;

    [Header("Visual Feedback")]
    [SerializeField] protected Color hitFlashColor = new Color(1f, 0.35f, 0.35f, 1f);
    [SerializeField] protected float hitFlashDuration = 0.12f;

    [Header("Gizmos")]
    [SerializeField] protected bool showGizmos = true;
    [SerializeField] protected Color detectionGizmoColor = new Color(1f, 0.75f, 0.2f, 0.65f);

    [Header("Events")]
    public UnityEvent<Transform> onTargetDetected;
    public UnityEvent onTargetLost;
    public UnityEvent<float> onDamaged;
    public UnityEvent onDied;

    protected Rigidbody2D rb;
    protected Collider2D enemyCollider;
    protected SpriteRenderer spriteRenderer;
    protected Transform currentTarget;
    protected Vector2 homePosition;
    protected Vector2 facingDirection = Vector2.right;
    protected Vector3 baseScale = Vector3.one;
    protected bool isDead;

    private Color m_defaultSpriteColor = Color.white;
    private float m_hitFlashTimer;
    private float m_invulnerableUntil;
    private float m_nextContactDamageTime;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public bool IsDead => isDead;
    public Transform CurrentTarget => currentTarget;
    public Vector2 FacingDirection => facingDirection;

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        enemyCollider = GetComponent<Collider2D>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        homePosition = transform.position;
        baseScale = transform.localScale;
        currentHealth = Mathf.Max(1f, maxHealth);

        if (spriteRenderer != null)
        {
            m_defaultSpriteColor = spriteRenderer.color;
        }

        OnEnemyAwake();
    }

    protected virtual void Reset()
    {
        SetupDefaultEnemyObjects();
    }

    /// <summary>
    /// Creates/wires default child GameObjects, visuals, and colliders in the Inspector.
    /// Can be triggered automatically on Add Component or from the Inspector button.
    /// </summary>
    public virtual void SetupDefaultEnemyObjects()
    {
        rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.freezeRotation = true;
        }
    }

    protected static Sprite EnsureSpriteAssigned(SpriteRenderer sr, EnemyShapeKind kind, Color defaultTint)
    {
        if (sr == null) return null;

        if (sr.sprite == null)
        {
            Sprite loaded = Resources.Load<Sprite>($"Sprites/Enemies/{kind}");
            sr.sprite = loaded != null ? loaded : GetOrCreateRuntimeShapeSprite(kind);
        }

        if (sr.color == Color.white)
        {
            sr.color = defaultTint;
        }

        return sr.sprite;
    }

    public static Sprite GetOrCreateRuntimeShapeSprite(EnemyShapeKind kind)
    {
        if (s_cachedShapeSprites.TryGetValue(kind, out Sprite cached) && cached != null)
            return cached;

        Texture2D tex = BuildShapeTexture(kind, 64);
        Sprite sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 64f);
        sprite.name = $"Enemy_{kind}_RuntimeSprite";
        s_cachedShapeSprites[kind] = sprite;
        return sprite;
    }

    public static Texture2D BuildShapeTexture(EnemyShapeKind kind, int size = 64)
    {
        int width = kind == EnemyShapeKind.ShieldBar ? Mathf.Max(8, size / 4) : size;
        int height = size;

        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        Color clear = new Color(0f, 0f, 0f, 0f);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                bool inside = false;
                if (kind == EnemyShapeKind.Square || kind == EnemyShapeKind.ShieldBar)
                {
                    inside = x >= 1 && x < width - 1 && y >= 1 && y < height - 1;
                }
                else if (kind == EnemyShapeKind.Circle)
                {
                    float nx = ((x + 0.5f) / width) * 2f - 1f;
                    float ny = ((y + 0.5f) / height) * 2f - 1f;
                    inside = (nx * nx + ny * ny) <= 0.92f;
                }
                else if (kind == EnemyShapeKind.Triangle)
                {
                    float ny = (float)y / (height - 1);
                    float halfSpan = (1f - ny) * (width * 0.45f);
                    float center = (width - 1) * 0.5f;
                    inside = y >= 2 && Mathf.Abs(x - center) <= halfSpan;
                }

                tex.SetPixel(x, y, inside ? Color.white : clear);
            }
        }

        tex.Apply();
        return tex;
    }

    protected virtual void OnEnemyAwake() { }

    protected virtual void Update()
    {
        if (isDead) return;

        UpdateHitFlash(Time.deltaTime);
        UpdateTargetDetection();
        UpdateEnemyBehavior(Time.deltaTime);
    }

    protected virtual void FixedUpdate()
    {
        if (isDead) return;

        FixedUpdateEnemyBehavior(Time.fixedDeltaTime);
    }

    protected abstract void UpdateEnemyBehavior(float dt);

    protected virtual void FixedUpdateEnemyBehavior(float fixedDt) { }

    #region Target Detection

    protected virtual void UpdateTargetDetection()
    {
        if (currentTarget != null)
        {
            if (!currentTarget.gameObject.activeInHierarchy)
            {
                ClearCurrentTarget();
                return;
            }

            float dist = Vector2.Distance(transform.position, currentTarget.position);
            float maxKeepRange = Mathf.Max(detectionRange, loseTargetRange);
            if (dist > maxKeepRange || (requireLineOfSight && !HasLineOfSight(currentTarget)))
            {
                ClearCurrentTarget();
                return;
            }
        }
        else
        {
            Transform found = FindBestTargetInRange();
            if (found != null)
            {
                currentTarget = found;
                onTargetDetected?.Invoke(currentTarget);
            }
        }
    }

    protected Transform FindBestTargetInRange()
    {
        if (string.IsNullOrWhiteSpace(targetTag))
            return null;

        GameObject[] candidates;
        try
        {
            candidates = GameObject.FindGameObjectsWithTag(targetTag);
        }
        catch (UnityException)
        {
            return null;
        }

        Transform best = null;
        float bestSqrDist = detectionRange * detectionRange;
        Vector2 myPos = transform.position;

        for (int i = 0; i < candidates.Length; i++)
        {
            GameObject candidate = candidates[i];
            if (candidate == null || !candidate.activeInHierarchy || candidate == gameObject)
                continue;

            float sqrDist = ((Vector2)candidate.transform.position - myPos).sqrMagnitude;
            if (sqrDist > bestSqrDist)
                continue;

            if (requireLineOfSight && !HasLineOfSight(candidate.transform))
                continue;

            bestSqrDist = sqrDist;
            best = candidate.transform;
        }

        return best;
    }

    protected bool HasLineOfSight(Transform target)
    {
        if (target == null) return false;

        Vector2 origin = transform.position;
        Vector2 toTarget = (Vector2)target.position - origin;
        float dist = toTarget.magnitude;
        if (dist <= 0.001f) return true;

        RaycastHit2D[] hits = Physics2D.RaycastAll(origin, toTarget / dist, dist, lineOfSightBlockers);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D col = hits[i].collider;
            if (col == null || col.isTrigger)
                continue;
            if (col.transform == transform || col.transform.IsChildOf(transform))
                continue;
            if (col.transform == target || col.transform.IsChildOf(target) || target.IsChildOf(col.transform))
                return true;

            return false;
        }

        return true;
    }

    private void ClearCurrentTarget()
    {
        currentTarget = null;
        onTargetLost?.Invoke();
    }

    protected void SetFacingDirection(Vector2 dir)
    {
        if (dir.sqrMagnitude <= 0.0001f)
            return;

        facingDirection = dir.normalized;
        if (spriteRenderer != null && Mathf.Abs(facingDirection.x) > 0.05f)
        {
            spriteRenderer.flipX = facingDirection.x < 0f;
        }
    }

    #endregion

    #region Damage, Knockback & Death

    public virtual bool TakeDamage(float amount, Vector2 hitImpulse = default, Vector2 hitSourceWorldPos = default)
    {
        if (isDead || amount <= 0f || Time.time < m_invulnerableUntil)
            return false;

        currentHealth = Mathf.Max(0f, currentHealth - amount);
        m_invulnerableUntil = Time.time + Mathf.Max(0f, hitInvulnerabilityDuration);
        TriggerHitFlash();

        if (rb != null && rb.bodyType == RigidbodyType2D.Dynamic && hitImpulse.sqrMagnitude > 0.0001f)
        {
            float knockScale = 1f - Mathf.Clamp01(knockbackResistance);
            if (knockScale > 0.001f)
            {
                rb.AddForce(hitImpulse * knockScale, ForceMode2D.Impulse);
            }
        }

        onDamaged?.Invoke(amount);

        if (currentHealth <= 0f)
        {
            Die();
        }

        return true;
    }

    /// <summary>
    /// Overload compatible with SendMessage("TakeDamage", floatAmount).
    /// </summary>
    public void TakeDamage(float amount)
    {
        TakeDamage(amount, Vector2.zero, transform.position);
    }

    public virtual void Heal(float amount)
    {
        if (isDead || amount <= 0f)
            return;

        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
    }

    protected virtual void Die()
    {
        if (isDead) return;
        isDead = true;

        onDied?.Invoke();

        if (enemyCollider != null)
            enemyCollider.enabled = false;

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        if (destroyOnDeath)
        {
            Destroy(gameObject, Mathf.Max(0f, deathDestroyDelay));
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    public virtual void ResetEnemy()
    {
        isDead = false;
        currentHealth = Mathf.Max(1f, maxHealth);
        transform.position = homePosition;
        transform.localScale = baseScale;
        currentTarget = null;
        m_invulnerableUntil = 0f;

        if (enemyCollider != null)
            enemyCollider.enabled = true;

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        if (spriteRenderer != null)
            spriteRenderer.color = m_defaultSpriteColor;

        gameObject.SetActive(true);
    }

    protected void TriggerHitFlash()
    {
        m_hitFlashTimer = Mathf.Max(0.02f, hitFlashDuration);
        if (spriteRenderer != null)
        {
            spriteRenderer.color = hitFlashColor;
        }
    }

    private void UpdateHitFlash(float dt)
    {
        if (m_hitFlashTimer <= 0f)
            return;

        m_hitFlashTimer -= dt;
        if (m_hitFlashTimer <= 0f && spriteRenderer != null)
        {
            spriteRenderer.color = m_defaultSpriteColor;
        }
    }

    #endregion

    #region Contact Damage

    protected virtual void OnCollisionEnter2D(Collision2D collision)
    {
        TryDealContactDamage(collision.collider);
    }

    protected virtual void OnCollisionStay2D(Collision2D collision)
    {
        TryDealContactDamage(collision.collider);
    }

    protected virtual void TryDealContactDamage(Collider2D other)
    {
        if (!dealContactDamage || isDead || other == null || Time.time < m_nextContactDamageTime)
            return;

        if (!MatchesTargetTag(other.gameObject))
            return;

        m_nextContactDamageTime = Time.time + Mathf.Max(0.05f, contactDamageCooldown);
        ApplyDamageAndKnockbackToTarget(other.gameObject, contactDamage, contactKnockbackForce, transform.position);
    }

    protected bool MatchesTargetTag(GameObject obj)
    {
        if (obj == null || string.IsNullOrWhiteSpace(targetTag))
            return false;

        if (obj.CompareTag(targetTag))
            return true;

        Transform root = obj.transform.root;
        return root != null && root.gameObject.CompareTag(targetTag);
    }

    public static void ApplyDamageAndKnockbackToTarget(GameObject targetObj, float damage, float knockbackForce, Vector3 sourceWorldPos)
    {
        if (targetObj == null) return;

        GameObject rootObj = targetObj.transform.root != null ? targetObj.transform.root.gameObject : targetObj;

        if (damage > 0f)
        {
            targetObj.SendMessage("TakeDamage", damage, SendMessageOptions.DontRequireReceiver);
            if (rootObj != targetObj)
            {
                rootObj.SendMessage("TakeDamage", damage, SendMessageOptions.DontRequireReceiver);
            }
        }

        if (knockbackForce > 0f)
        {
            Rigidbody2D targetRb = targetObj.GetComponentInParent<Rigidbody2D>();
            if (targetRb != null && targetRb.bodyType == RigidbodyType2D.Dynamic)
            {
                Vector2 knockDir = ((Vector2)targetRb.position - (Vector2)sourceWorldPos);
                if (knockDir.sqrMagnitude <= 0.0001f) knockDir = Vector2.up;
                targetRb.AddForce(knockDir.normalized * knockbackForce, ForceMode2D.Impulse);
            }
        }
    }

    #endregion

    #region Gizmos

    protected virtual void OnDrawGizmosSelected()
    {
        if (!showGizmos) return;
        DrawEnemyGizmos();
    }

    protected virtual void DrawEnemyGizmos()
    {
        Vector3 pos = transform.position;

        Gizmos.color = detectionGizmoColor;
        DrawWireCircle2D(pos, Mathf.Max(0f, detectionRange), 36);

        if (loseTargetRange > detectionRange)
        {
            Gizmos.color = new Color(detectionGizmoColor.r, detectionGizmoColor.g, detectionGizmoColor.b, 0.25f);
            DrawWireCircle2D(pos, loseTargetRange, 36);
        }

        if (currentTarget != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(pos, currentTarget.position);
        }
    }

    protected static void DrawWireCircle2D(Vector2 center, float radius, int segments)
    {
        if (radius <= 0f || segments < 3) return;

        float angleStep = (Mathf.PI * 2f) / segments;
        Vector3 prev = new Vector3(center.x + radius, center.y, 0f);
        for (int i = 1; i <= segments; i++)
        {
            float a = i * angleStep;
            Vector3 next = new Vector3(center.x + Mathf.Cos(a) * radius, center.y + Mathf.Sin(a) * radius, 0f);
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }

    #endregion
}

