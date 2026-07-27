using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem; // Required for the New Input System

public class PlayerInput : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera mainCamera;

    [Header("Input Tuning")]
    [SerializeField] private float minDragDistance = 0.2f; // Minimum drag in world units

    [SerializeField] private MultiGridManager multiGridManager;
    private Block selectedBlock;
    private Vector3 startTouchWorldPos;
    private bool isDragging = false;

    [SerializeField] private GravityManager gravityManager;

    private bool isProcessingTurn = false; // Lock input while falling animations play
    public bool IsProcessingTurn => isProcessingTurn;
    private void Awake()
    {
        if (mainCamera == null) mainCamera = Camera.main;
    }

    private void Update()
    {
        HandleInput();
    }

    private void HandleInput()
    {
        // 1. Get current active pointer (Mouse, Touch, or Stylus)
        Pointer currentPointer = Pointer.current;
        if (currentPointer == null) return;

        // 2. Read screen position and convert to world space
        Vector2 screenPos = currentPointer.position.ReadValue();
        Vector3 worldPos = mainCamera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, -mainCamera.transform.position.z));

        // 3. PRESS STARTED (Primary touch/click pressed this frame)
        if (currentPointer.press.wasPressedThisFrame)
        {
            Vector2 mousePos2D = new Vector2(worldPos.x, worldPos.y);
            RaycastHit2D hit = Physics2D.Raycast(mousePos2D, Vector2.zero);

            if (hit.collider != null)
            {
                if (hit.collider.TryGetComponent<Block>(out Block block))
                {
                    // Ignore immovable blocks directly
                    if (!block.CanPlayerMoveDirectly()) return;

                    selectedBlock = block;
                    startTouchWorldPos = worldPos;
                    isDragging = true;
                }
            }
        }

        // 4. DRAG IN PROGRESS (Primary touch/click held down)
        if (currentPointer.press.isPressed && isDragging && selectedBlock != null)
        {
            Vector3 dragVector = worldPos - startTouchWorldPos;

            // Trigger move once drag exceeds threshold
            if (dragVector.magnitude >= minDragDistance)
            {
                Vector2Int direction = GetSwipeDirection(dragVector);
                AttemptMove(selectedBlock, direction);

                // Reset drag state so it only triggers once per swipe
                isDragging = false;
                selectedBlock = null;
            }
        }

        // 5. PRESS RELEASED
        if (currentPointer.press.wasReleasedThisFrame)
        {
            isDragging = false;
            selectedBlock = null;
        }
    }

    private Vector2Int GetSwipeDirection(Vector3 dragVector)
    {
        if (Mathf.Abs(dragVector.x) > Mathf.Abs(dragVector.y))
        {
            return dragVector.x > 0 ? Vector2Int.right : Vector2Int.left;
        }
        else
        {
            return dragVector.y > 0 ? Vector2Int.up : Vector2Int.down;
        }
    }

    private void AttemptMove(Block block, Vector2Int direction)
    {
        if (isProcessingTurn) return;

        GridIsland sourceIsland = block.currentIsland;
        if (sourceIsland == null) return;

        // ... (Your JointBlock pair move checks) ...

        Vector2Int currentLocalPos = block.gridPosition;
        Vector2Int targetLocalPos = currentLocalPos + direction;

        if (sourceIsland.IsValidLocalPos(targetLocalPos))
        {
            if (sourceIsland.CanMoveBlockLocal(currentLocalPos, targetLocalPos))
            {
                sourceIsland.ExecuteMoveLocal(currentLocalPos, targetLocalPos);
                block.MoveToGridPosition(targetLocalPos);

                // Start gravity cascade after player move
                StartCoroutine(PostMoveRoutine());
            }
            else
            {
                block.PlayIllegalMoveAnimation(direction);
            }
        }
        else
        {
            // Inter-island gap logic
            Vector2Int currentWorldPos = sourceIsland.originPosition + currentLocalPos;
            Vector2Int targetWorldPos = currentWorldPos + direction;

            if (multiGridManager.TryTransferBlockBetweenIslands(block, sourceIsland, targetWorldPos))
            {
                GridIsland targetIsland = block.currentIsland;
                Vector2Int newLocalPos = targetIsland.WorldToLocal(targetWorldPos);
                block.MoveToGridPosition(newLocalPos);

                StartCoroutine(PostMoveRoutine());
            }
            else
            {
                block.PlayIllegalMoveAnimation(direction);
            }
        }
    }

    private IEnumerator PostMoveRoutine()
    {
        isProcessingTurn = true;

        // Wait for the swipe animation to finish
        yield return new WaitForSeconds(0.2f);

        // Run gravity routine
        if (gravityManager != null && multiGridManager != null)
        {
            yield return StartCoroutine(gravityManager.ApplyGravityRoutine(multiGridManager.GetActiveIslands()));
        }
        else
        {
            Debug.LogWarning("GravityManager or MultiGridManager reference missing on PlayerInput!");
        }

        isProcessingTurn = false;
    }
}