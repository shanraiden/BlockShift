using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInput : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private MultiGridManager multiGridManager;
    [SerializeField] private GravityManager gravityManager;

    [Header("Input Tuning")]
    [SerializeField] private float minDragDistance = 0.2f;

    private Block selectedBlock;
    private JointBlock activeJointBlock;
    private Vector3 startTouchWorldPos;
    private bool isDragging = false;

    private bool isProcessingTurn = false;
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
        if (isProcessingTurn) return;

        Pointer currentPointer = Pointer.current;
        if (currentPointer == null) return;

        Vector2 screenPos = currentPointer.position.ReadValue();
        Vector3 worldPos = mainCamera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, -mainCamera.transform.position.z));

        // 1. PRESS STARTED
        if (currentPointer.press.wasPressedThisFrame)
        {
            Vector2 mousePos2D = new Vector2(worldPos.x, worldPos.y);
            RaycastHit2D hit = Physics2D.Raycast(mousePos2D, Vector2.zero);

            if (hit.collider != null)
            {
                if (hit.collider.TryGetComponent<Block>(out Block block))
                {
                    // Case A: DIRECT TAP ON AN IMMOVABLE BLOCK
                    if (block is ImmovableBlock immovable)
                    {
                        List<JointBlock> adjacentJoints = immovable.GetAdjacentJointBlocks();

                        if (adjacentJoints.Count > 0)
                        {
                            // If switching to a new joint, deselect old pair
                            JointBlock targetJoint = adjacentJoints[0];
                            if (activeJointBlock != null && activeJointBlock != targetJoint)
                            {
                                DeselectCurrentPair();
                            }

                            activeJointBlock = targetJoint;
                            // Toggle this immovable block in/out of activeJointBlock's multi-link list
                            activeJointBlock.ToggleLinkImmovableBlock(immovable);

                            selectedBlock = immovable;
                            startTouchWorldPos = worldPos;
                            isDragging = true;
                            return;
                        }
                    }

                    // Case B: DIRECT TAP/SWIPE ON A JOINT BLOCK
                    if (block is JointBlock joint)
                    {
                        if (activeJointBlock != null && activeJointBlock != joint)
                        {
                            DeselectCurrentPair();
                        }

                        activeJointBlock = joint;
                        selectedBlock = joint;
                        startTouchWorldPos = worldPos;
                        isDragging = true;
                        return;
                    }

                    // Case C: STANDARD MOVABLE BLOCK
                    if (block.CanPlayerMoveDirectly())
                    {
                        DeselectCurrentPair();

                        selectedBlock = block;
                        startTouchWorldPos = worldPos;
                        isDragging = true;
                    }
                }
            }
            else
            {
                // Tapped empty space -> Deselect all
                DeselectCurrentPair();
            }
        }

        // 2. DRAG IN PROGRESS (Swipe Detection)
        if (currentPointer.press.isPressed && isDragging && selectedBlock != null)
        {
            Vector3 dragVector = worldPos - startTouchWorldPos;

            if (dragVector.magnitude >= minDragDistance)
            {
                Vector2Int direction = GetSwipeDirection(dragVector);
                AttemptMove(selectedBlock, direction);

                isDragging = false;
                selectedBlock = null;
            }
        }

        // 3. PRESS RELEASED
        if (currentPointer.press.wasReleasedThisFrame)
        {
            isDragging = false;
            selectedBlock = null;
        }
    }

    private void DeselectCurrentPair()
    {
        if (activeJointBlock != null)
        {
            activeJointBlock.DeselectAll();
            activeJointBlock = null;
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

        // --- JOINT BLOCK OR LINKED IMMOVABLE BLOCK MOVE ---
        if (block is JointBlock jointBlock)
        {
            if (jointBlock.TryMoveWithSelectedImmovables(direction))
            {
                StartCoroutine(PostMoveRoutine());
            }
            else
            {
                block.PlayIllegalMoveAnimation(direction);
            }
            return;
        }

        if (block is ImmovableBlock && activeJointBlock != null)
        {
            if (activeJointBlock.TryMoveWithSelectedImmovables(direction))
            {
                StartCoroutine(PostMoveRoutine());
            }
            else
            {
                block.PlayIllegalMoveAnimation(direction);
            }
            return;
        }

        // --- STANDARD CASE: REGULAR BLOCK ---
        Vector2Int currentLocalPos = block.gridPosition;
        Vector2Int targetLocalPos = currentLocalPos + direction;

        if (sourceIsland.IsValidLocalPos(targetLocalPos))
        {
            if (sourceIsland.CanMoveBlockLocal(currentLocalPos, targetLocalPos))
            {
                sourceIsland.ExecuteMoveLocal(currentLocalPos, targetLocalPos);
                block.MoveToGridPosition(targetLocalPos);

                StartCoroutine(PostMoveRoutine());
            }
            else
            {
                block.PlayIllegalMoveAnimation(direction);
            }
        }
        else
        {
            // Inter-island gap transfer logic
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
        yield return new WaitForSeconds(0.2f);

        if (gravityManager != null && multiGridManager != null)
        {
            yield return StartCoroutine(gravityManager.ApplyGravityRoutine(multiGridManager.GetActiveIslands()));
        }

        isProcessingTurn = false;
    }
}