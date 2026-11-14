using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class PlayerMovement : MonoBehaviour
{
    [Header("UI Touch Buttons")]
    [Tooltip("Reference to UI Buttons for screen left/right movement")]
    [SerializeField] private Button touchContactActionLeft;
    [SerializeField] private Button touchContactActionRight;

    [Header("New Input System Actions (Keyboard / Gamepad)")]
    [Tooltip("Assign Keyboard (e.g. A / LeftArrow) or Gamepad (e.g. D-Pad Left / Left Stick Left) bindings to move left.")]
    public InputAction moveLeftAction = new InputAction("Move Left", InputActionType.Button);

    [Tooltip("Assign Keyboard (e.g. D / RightArrow) or Gamepad (e.g. D-Pad Right / Left Stick Right) bindings to move right.")]
    public InputAction moveRightAction = new InputAction("Move Right", InputActionType.Button);

    [Tooltip("Optional Input Action to trigger IsRunning instead of IsMoving while moving.")]
    public InputAction runModifierAction = new InputAction("Run Modifier", InputActionType.Button);

    [Header("Movement Settings")]
    [Tooltip("Constant horizontal speed applied when moving left or right.")]
    [SerializeField] private float moveSpeed = 10f;

    [Tooltip("Extra speed added to moveSpeed when running (total running speed = moveSpeed + runSpeedBonus).")]
    [SerializeField] private float runSpeedBonus = 6f;

    [Tooltip("If true, compares touch against Screen Center (Screen.width / 2). If false, compares against Control Area center.")]
    [SerializeField] private bool useScreenCenter = false;

    public Rigidbody2D PlayerRigidBody;
    public RectTransform controlAreaRect;

    [Header("Procedural FK Legs Control")]
    [Tooltip("List of ProceduralFKLeg2D components controlled by Left/Right/No input.")]
    public List<ProceduralFKLeg2D> proceduralFKLegs = new List<ProceduralFKLeg2D>();

    [Tooltip("If true, Left/Right input sets IsRunning = true on all legs even without holding Run Modifier.")]
    public bool triggerRunningOnMove = false;

    [Tooltip("If true, flips transform.localScale.x when changing direction (ideal for 2D bone rigs).")]
    public bool flipTransformScaleX = false;

    [Header("Status (Read-Only)")]
    [SerializeField] private bool isStopped = true;
    [SerializeField] private bool isRunning = false;

    private int uiMovingDirection = 0;
    private int playerMovingDirection = 0;
    private SpriteRenderer spriteRenderer;

    private void Awake()
    {
        if (PlayerRigidBody == null)
            PlayerRigidBody = GetComponent<Rigidbody2D>();

        spriteRenderer = GetComponent<SpriteRenderer>();

        // Auto-populate default Keyboard & Gamepad bindings if none were configured in the Inspector
        if (moveLeftAction.bindings.Count == 0)
        {
            moveLeftAction.AddBinding("<Keyboard>/a");
            moveLeftAction.AddBinding("<Keyboard>/leftArrow");
            moveLeftAction.AddBinding("<Gamepad>/dpad/left");
            moveLeftAction.AddBinding("<Gamepad>/leftStick/left");
        }

        if (moveRightAction.bindings.Count == 0)
        {
            moveRightAction.AddBinding("<Keyboard>/d");
            moveRightAction.AddBinding("<Keyboard>/rightArrow");
            moveRightAction.AddBinding("<Gamepad>/dpad/right");
            moveRightAction.AddBinding("<Gamepad>/leftStick/right");
        }

        if (runModifierAction.bindings.Count == 0)
        {
            runModifierAction.AddBinding("<Keyboard>/leftShift");
            runModifierAction.AddBinding("<Gamepad>/buttonSouth");
        }

        if (proceduralFKLegs == null || proceduralFKLegs.Count == 0)
        {
            GetComponentsInChildren(true, proceduralFKLegs);
        }
    }

    private void OnEnable()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();

        GameObject controlAreaObj = GameObject.Find("PlayerControlArea");
        if (controlAreaObj != null)
        {
            controlAreaRect = controlAreaObj.GetComponent<RectTransform>();
        }

        moveLeftAction?.Enable();
        moveRightAction?.Enable();
        runModifierAction?.Enable();
    }

    private void OnDisable()
    {
        moveLeftAction?.Disable();
        moveRightAction?.Disable();
        runModifierAction?.Disable();
    }

    public void velocityDirectionSetLeft()
    {
        uiMovingDirection = -1;
    }

    public void velocityDirectionUnSetLeft()
    {
        if (uiMovingDirection < 0)
            uiMovingDirection = 0;
    }

    public void velocityDirectionSetRight()
    {
        uiMovingDirection = 1;
    }

    public void velocityDirectionUnSetRight()
    {
        if (uiMovingDirection > 0)
            uiMovingDirection = 0;
    }

    private void Update()
    {
        // 1. Read New Input System actions (Keyboard / Gamepad) and combine with UI touch buttons
        bool leftPressed = moveLeftAction != null && moveLeftAction.IsPressed();
        bool rightPressed = moveRightAction != null && moveRightAction.IsPressed();

        int inputSystemDir = 0;
        if (leftPressed && !rightPressed)
            inputSystemDir = -1;
        else if (rightPressed && !leftPressed)
            inputSystemDir = 1;

        playerMovingDirection = inputSystemDir != 0 ? inputSystemDir : uiMovingDirection;
        isStopped = (playerMovingDirection == 0);

        // 2. Update ProceduralFKLeg2D states (IsMoving / IsRunning / IsStill) based on Left, Right, or No Input
        bool isSprintHeld = runModifierAction != null && runModifierAction.IsPressed();
        UpdateProceduralFKLegs(playerMovingDirection, isSprintHeld);

        // 3. Update character facing visuals when moving
        if (playerMovingDirection != 0)
        {
            HandleDirectionalVisuals(playerMovingDirection);
        }
    }

    private void FixedUpdate()
    {
        ApplyVelocityMovement();
    }

    private void UpdateProceduralFKLegs(int direction, bool isSprintHeld)
    {
        bool hasMovementInput = (direction != 0);
        bool shouldRun = hasMovementInput && (triggerRunningOnMove || isSprintHeld);
        bool shouldMove = hasMovementInput && !shouldRun;
        isRunning = shouldRun;

        if (proceduralFKLegs == null || proceduralFKLegs.Count == 0) return;

        for (int i = 0; i < proceduralFKLegs.Count; i++)
        {
            ProceduralFKLeg2D leg = proceduralFKLegs[i];
            if (leg == null) continue;

            leg.IsStill = !hasMovementInput;
            leg.IsMoving = shouldMove;
            leg.IsRunning = shouldRun;
        }
    }

    private void HandleDirectionalVisuals(float direction)
    {
        if (Mathf.Abs(direction) < 0.01f) return;

        bool movingRight = direction > 0;

        if (spriteRenderer != null)
        {
            spriteRenderer.flipX = !movingRight;
        }

        if (flipTransformScaleX)
        {
            Vector3 scale = transform.localScale;
            float absX = Mathf.Abs(scale.x);
            scale.x = movingRight ? -absX : absX;
            transform.localScale = scale;
        }
    }

    private void ApplyVelocityMovement()
    {
        if (PlayerRigidBody == null) return;

        float activeSpeed = isRunning ? (moveSpeed + runSpeedBonus) : moveSpeed;

        // Apply constant left/right velocity while retaining vertical (y) momentum
        PlayerRigidBody.linearVelocity = new Vector2(playerMovingDirection * activeSpeed, PlayerRigidBody.linearVelocity.y);
    }
}