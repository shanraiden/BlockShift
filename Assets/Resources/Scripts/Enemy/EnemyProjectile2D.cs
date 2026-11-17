using UnityEngine;

/// <summary>
/// 2D projectile thrown by TriangleEnemy (or other ranged enemies).
/// Supports linear flight or gravity-affected lobbing, target damage,
/// knockback, and deflection against blocking SquareEnemy shields.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class EnemyProjectile2D : MonoBehaviour
{
    [Header("Projectile Settings")]
    [SerializeField] private float damage = 1f;
    [SerializeField] private float knockbackForce = 5f;
    [SerializeField] private float lifetime = 4f;
    [SerializeField] private float spinSpeed = 360f;
    [SerializeField] private string targetTag = "Player";
    [SerializeField] private LayerMask environmentMask = ~0;
    [SerializeField] private bool destroyOnEnvironmentHit = true;

    private Rigidbody2D rb;
    private Collider2D projCollider;
    private GameObject ownerObject;
    private float spawnTime;
    private bool initialized;

    public void Initialize(
        GameObject owner,
        Vector2 initialVelocity,
        float projectileDamage,
        float projectileKnockback,
        float projectileGravityScale,
        float maxLifetime,
        string desiredTargetTag)
    {
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        if (projCollider == null) projCollider = GetComponent<Collider2D>();

        ownerObject = owner;
        damage = projectileDamage;
        knockbackForce = projectileKnockback;
        lifetime = Mathf.Max(0.2f, maxLifetime);
        targetTag = string.IsNullOrWhiteSpace(desiredTargetTag) ? "Player" : desiredTargetTag;

        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = projectileGravityScale;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.linearVelocity = initialVelocity;

        if (projCollider != null)
        {
            projCollider.isTrigger = true;
            if (owner != null)
            {
                Collider2D[] ownerCols = owner.GetComponentsInChildren<Collider2D>();
                for (int i = 0; i < ownerCols.Length; i++)
                {
                    if (ownerCols[i] != null)
                        Physics2D.IgnoreCollision(projCollider, ownerCols[i], true);
                }
            }
        }

        spawnTime = Time.time;
        initialized = true;
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        projCollider = GetComponent<Collider2D>();
        if (!initialized)
        {
            spawnTime = Time.time;
        }
    }

    private void Update()
    {
        if (Time.time - spawnTime >= lifetime)
        {
            Destroy(gameObject);
            return;
        }

        if (Mathf.Abs(spinSpeed) > 0.01f)
        {
            transform.Rotate(0f, 0f, spinSpeed * Time.deltaTime);
        }
        else if (rb != null && rb.linearVelocity.sqrMagnitude > 0.01f)
        {
            float angle = Mathf.Atan2(rb.linearVelocity.y, rb.linearVelocity.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        HandleHit(other);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        HandleHit(collision.collider);
    }

    private void HandleHit(Collider2D other)
    {
        if (other == null) return;

        if (ownerObject != null && (other.gameObject == ownerObject || other.transform.IsChildOf(ownerObject.transform)))
            return;

        // If hitting a blocking SquareEnemy shield, deflect/destroy the projectile cleanly
        SquareEnemy square = other.GetComponentInParent<SquareEnemy>();
        if (square != null)
        {
            bool absorbed = !square.TakeDamage(damage, rb != null ? rb.linearVelocity : Vector2.zero, transform.position);
            if (absorbed)
            {
                if (rb != null)
                {
                    rb.linearVelocity = -rb.linearVelocity * 0.7f;
                }
                Destroy(gameObject, 0.15f);
                return;
            }
        }

        // Hit target (e.g. Player)
        Transform root = other.transform.root;
        bool isTarget = (!string.IsNullOrWhiteSpace(targetTag)) &&
                        (other.CompareTag(targetTag) || (root != null && root.CompareTag(targetTag)));

        if (isTarget)
        {
            Enemy.ApplyDamageAndKnockbackToTarget(other.gameObject, damage, knockbackForce, transform.position);
            Destroy(gameObject);
            return;
        }

        // Ignore triggers (e.g., portals, BlackEternal shadow zones) unless solid environment
        if (other.isTrigger)
            return;

        if (destroyOnEnvironmentHit && ((environmentMask.value & (1 << other.gameObject.layer)) != 0))
        {
            Destroy(gameObject);
        }
    }
}

