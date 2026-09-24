using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Animator))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private List<Transform> pathWaypoints = new List<Transform>();
    [SerializeField] private float waypointThreshold = 0.15f;
    [SerializeField] private bool loopPath = true;

    [Header("Facing Settings")]
    [SerializeField] private bool rotateTowardsTarget = false; // Set TRUE if top-down, FALSE if side-scroller (flips X)
    [SerializeField] private float rotationSpeed = 10f;

    [Header("Obstacle Detection")]
    [SerializeField] private string obstacleTag = "Obstacle";
    [SerializeField] private float raycastDistance = 1.5f;
    [SerializeField] private LayerMask obstacleLayerMask = ~0;

    [Header("Hover Settings (Idle Only)")]
    [SerializeField] private float hoverAmplitude = 0.15f;
    [SerializeField] private float hoverFrequency = 3f;

    [Header("Animation Clips")]
    [SerializeField] private AnimationClip idleClip;
    [SerializeField] private AnimationClip runClip;

    [Header("Status (Read-Only)")]
    [SerializeField] private bool isStopped;

    // Component References
    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private Animator animator;
    private Collider2D playerCollider;

    // Internal Movement State
    private int currentWaypointIndex = 0;
    private Vector2 moveDirection = Vector2.right;

    // Hover State
    private float hoverTimer;

    // Animation State
    private AnimationClip currentClip;

    // Hash references for Animator Controller parameters
    private static readonly int IsMovingHash = Animator.StringToHash("isMoving");
    private static readonly int IsStoppedHash = Animator.StringToHash("isStopped");
    private static readonly int SpeedHash = Animator.StringToHash("speed");

    public bool IsStopped => isStopped;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
        playerCollider = GetComponent<Collider2D>();

        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.bodyType = RigidbodyType2D.Dynamic;
    }

    private void Update()
    {
        FollowPath();
        CheckForObstacles();
       
        UpdateAnimation();
    }

    private void FixedUpdate()
    {
        UpdateFacingDirection();
        ApplyVelocityMovement();
        HandleHoverEffect();
    }

    private void ApplyVelocityMovement()
    {
        if (!isStopped && pathWaypoints != null && pathWaypoints.Count > 0)
        {
            rb.linearVelocity = moveDirection * moveSpeed;
        }
        else
        {
            rb.linearVelocity = Vector2.zero;
        }
    }

    private void CheckForObstacles()
    {
        if (moveDirection.sqrMagnitude < 0.001f) return;

        RaycastHit2D hit = Physics2D.Raycast(rb.position, moveDirection, raycastDistance, obstacleLayerMask);

        if (hit.collider != null && hit.collider != playerCollider)
        {
            if (hit.collider.CompareTag(obstacleTag))
            {
                isStopped = true;
                return;
            }
        }

        isStopped = false;
    }

    private void FollowPath()
    {
        if (pathWaypoints == null || pathWaypoints.Count == 0)
        {
            isStopped = true;
            return;
        }

        // Boundary Guard: Exit immediately if path ended
        if (currentWaypointIndex >= pathWaypoints.Count)
        {
            if (loopPath)
            {
                currentWaypointIndex = 0;
            }
            else
            {
                isStopped = true;
                moveDirection = Vector2.zero;
                return;
            }
        }

        Transform currentTarget = pathWaypoints[currentWaypointIndex];
        if (currentTarget == null) return;

        Vector2 vectorToTarget = (Vector2)currentTarget.position - rb.position;

        // Waypoint reached check
        if (vectorToTarget.magnitude <= waypointThreshold)
        {
            currentWaypointIndex++;

            if (currentWaypointIndex >= pathWaypoints.Count)
            {
                if (loopPath)
                {
                    currentWaypointIndex = 0;
                }
                else
                {
                    isStopped = true;
                    moveDirection = Vector2.zero;
                    return;
                }
            }

            currentTarget = pathWaypoints[currentWaypointIndex];
            vectorToTarget = (Vector2)currentTarget.position - rb.position;
        }

        if (vectorToTarget.sqrMagnitude > 0.001f)
        {
            moveDirection = vectorToTarget.normalized;
        }
    }

    private void UpdateFacingDirection()
    {
        if (moveDirection.sqrMagnitude < 0.001f) return;

        if (rotateTowardsTarget)
        {
            // Smoothly rotate toward target angle (Ideal for top-down games)
            float targetAngle = Mathf.Atan2(moveDirection.y, moveDirection.x) * Mathf.Rad2Deg;
            Quaternion targetRotation = Quaternion.Euler(0f, 0f, targetAngle);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
        }
        else
        {
            // Horizontal Flip (Ideal for side-scrollers / 2.5D platforms)
            if (Mathf.Abs(moveDirection.x) > 0.01f)
            {
                Vector3 currentScale = transform.localScale;
                float absX = Mathf.Abs(currentScale.x);
                currentScale.x = moveDirection.x > 0 ? absX : -absX;
                transform.localScale = currentScale;
            }
        }
    }

    private void HandleHoverEffect()
    {
        if (!isStopped)
        {
            hoverTimer = 0f;
            return;
        }

        hoverTimer += Time.fixedDeltaTime * hoverFrequency;
        float yOffset = Mathf.Sin(hoverTimer) * hoverAmplitude;

        rb.position = new Vector2(rb.position.x, rb.position.y + (yOffset * Time.fixedDeltaTime));
    }

    private void UpdateAnimation()
    {
        if (animator == null) return;

        bool isMoving = !isStopped;

        if (HasParameter(IsMovingHash)) animator.SetBool(IsMovingHash, isMoving);
        if (HasParameter(IsStoppedHash)) animator.SetBool(IsStoppedHash, isStopped);
        if (HasParameter(SpeedHash)) animator.SetFloat(SpeedHash, isMoving ? moveSpeed : 0f);

        AnimationClip targetClip = isStopped ? idleClip : runClip;
        if (targetClip != null && currentClip != targetClip)
        {
            currentClip = targetClip;
            animator.Play(currentClip.name);
        }
    }

    private bool HasParameter(int paramHash)
    {
        foreach (AnimatorControllerParameter param in animator.parameters)
        {
            if (param.nameHash == paramHash) return true;
        }
        return false;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = isStopped ? Color.red : Color.green;
        Vector3 rayDir = moveDirection.sqrMagnitude > 0.001f ? (Vector3)moveDirection : transform.right;
        Gizmos.DrawLine(transform.position, transform.position + rayDir * raycastDistance);
        Gizmos.DrawWireSphere(transform.position + rayDir * raycastDistance, 0.1f);

        if (pathWaypoints != null && pathWaypoints.Count > 0)
        {
            Gizmos.color = Color.cyan;
            for (int i = 0; i < pathWaypoints.Count; i++)
            {
                if (pathWaypoints[i] == null) continue;

                // Highlight active target point in yellow
                Gizmos.color = (i == currentWaypointIndex) ? Color.yellow : Color.cyan;
                Gizmos.DrawSphere(pathWaypoints[i].position, 0.2f);

                int nextIndex = (i + 1) % pathWaypoints.Count;
                if (nextIndex < pathWaypoints.Count && pathWaypoints[nextIndex] != null)
                {
                    if (i < pathWaypoints.Count - 1 || loopPath)
                    {
                        Gizmos.color = Color.cyan;
                        Gizmos.DrawLine(pathWaypoints[i].position, pathWaypoints[nextIndex].position);
                    }
                }
            }
        }
    }
}