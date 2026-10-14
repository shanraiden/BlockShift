using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class PlayerMovement : MonoBehaviour
{
    [Header("Input Action Reference")]
    [Tooltip("Reference to an Input Action for screen contact/press (e.g. Touch/press or Mouse/leftButton)")]
    [SerializeField] private Button touchContactActionLeft;
    [SerializeField] private Button touchContactActionRight;


    [Header("Movement Settings")]
    [Tooltip("Constant speed applied when touching left or right.")]
    [SerializeField] private float moveSpeed = 10f;

    [Tooltip("If true, compares touch against Screen Center (Screen.width / 2). If false, compares against Control Area center.")]
    [SerializeField] private bool useScreenCenter = false;

    public Rigidbody2D PlayerRigidBody;
    public RectTransform controlAreaRect;
    private bool isTouching = false;
    private int playerMovingDirection;
    private SpriteRenderer spriteRenderer;
    [Header("Hover Settings")]
    public float targetHoverHeight = 2.0f;   // Desired distance from ground
    public float hoverAmplitude = 0.5f; // How high it bobs
    public float hoverFrequency = 2f;  // How fast it bobs
    public float maxDetectionRange = 5.0f;   // Max height before gravity takes over
    public float hoverStrength = 15.0f;      // How snappy the push is
    public LayerMask groundLayer;        // Set this to your "Ground" layer in the Inspector
    private float hoverTimer;
    public float hitOffset;
    private int WaterLayer;

    [Header("Animation Clips")]
    [SerializeField] private AnimationClip idleClip;
    [SerializeField] private AnimationClip runClip;
    private Animator animator;
    [Header("Status (Read-Only)")]
    [SerializeField] private bool isStopped;
    // Animation State
    private AnimationClip currentClip;

    // Hash references for Animator Controller parameters
    private static readonly int IsMovingHash = Animator.StringToHash("isMoving");
    private static readonly int IsStoppedHash = Animator.StringToHash("isStopped");
    private static readonly int SpeedHash = Animator.StringToHash("speed");
    private void Awake()
    {
        WaterLayer = LayerMask.NameToLayer("Water");
        animator = GetComponent<Animator>();
    }

    private void OnEnable()
    {
      //  touchContactActionLeft.OnSelect(playerVelocityButton);
      //  touchContactActionRight.OnSelect(playerVelocityButton);

        spriteRenderer = GetComponent<SpriteRenderer>();
        controlAreaRect = GameObject.Find("PlayerControlArea").GetComponent<RectTransform>();
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

    public void velocityDirectionSetLeft()
    {
        playerMovingDirection = -1;
    }
    public void velocityDirectionUnSetLeft()
    {
        playerMovingDirection = 0;
    }

    public void velocityDirectionSetRight()
    {
        playerMovingDirection = 1;
    }

    public void velocityDirectionUnSetRight()
    {
        playerMovingDirection = 0;
    }




    private void ApplyHover()
    {
        // 1. Cast a ray down to find the ground within our Max Range
        RaycastHit2D hit = Physics2D.Raycast(new Vector2(hitOffset + transform.position.x, transform.position.y), Vector2.down, maxDetectionRange, groundLayer);

        if (hit.collider != null)
        {

            if ((hit.collider.gameObject.layer != WaterLayer))
            {
                float currentDistance = hit.distance;
                if (PlayerRigidBody.gravityScale != 0) PlayerRigidBody.gravityScale = 0;
                if (currentDistance < maxDetectionRange)
                {
                    // --- 1. Update Timer & Oscillation ---
                    hoverTimer += Time.fixedDeltaTime;
                    float bobbingVelocityY = 0;
                    // Calculate the Cosine velocity for a smooth physical bob
                    bobbingVelocityY = Mathf.Cos(hoverTimer * hoverFrequency) * (hoverAmplitude * hoverFrequency);

                    // --- 2. Calculate Ground-Correction Velocity ---
                    // If we are too low/high relative to targetHoverHeight, we add a correction
                    float heightError = targetHoverHeight - currentDistance;
                    float correctionVelocityY = heightError * hoverStrength;

                    // --- 3. Merge and Apply Friction ---
                    // Target velocity is the bobbing + the correction to stay at height
                    Vector2 targetVelocity = new Vector2(0, bobbingVelocityY + correctionVelocityY);

                    // Smoothly interpolate to the target velocity (Friction/Damping)
                    float friction = 5f;
                    PlayerRigidBody.linearVelocity = Vector2.Lerp(PlayerRigidBody.linearVelocity, targetVelocity, Time.fixedDeltaTime * friction);
                }
            }
            else
            {
                PlayerRigidBody.gravityScale = 1f;
            }

            // --- 4. Update Shadow Visuals ---
        }
        else
        {
            PlayerRigidBody.gravityScale = 1; // Enable gravity when no ground is detected
            // Optional: Reset timer when not hovering so the wave starts fresh
            hoverTimer = 0;
        }
        // If hit.collider is null, the player is "Too High" and will simply fall
    }

   

    private void Update()
    {
        isStopped = (playerMovingDirection == 0) ? true : false;
        UpdateAnimation();
        if (playerMovingDirection == 0) return;
                HandleDirectionalVisuals(playerMovingDirection);
        
    }
    /// <summary>
    /// Checks if a World Space point is inside a RectTransform.
    /// </summary>
 
    private void FixedUpdate()
    {
        ApplyHover();
        ApplyVelocityMovement();
    }
    private void HandleDirectionalVisuals(float direction)
    {
        if (Mathf.Abs(direction) < 0.01f) return;

        bool movingRight = direction > 0;
        spriteRenderer.flipX = !movingRight; // Assuming sprite faces right by default
        // Ensure hitOffset correctly flips based on direction
        hitOffset = movingRight ? Mathf.Abs(hitOffset) * -1 : Mathf.Abs(hitOffset);
    }
    private void ApplyVelocityMovement()
    {
        if (PlayerRigidBody == null) return;

        // Apply constant left/right velocity while retaining vertical (y) momentum
       
        PlayerRigidBody.linearVelocity = new Vector2(playerMovingDirection*moveSpeed, PlayerRigidBody.linearVelocity.y);
    }

}