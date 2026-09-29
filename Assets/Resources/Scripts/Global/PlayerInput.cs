using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
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

                    // Case D: SPECIAL BLOCKS (LinkerBlock)

                    if (block is LinkerBlock linkBlk)
                    {
                        if (linkBlk.IsLocked)
                        {
                            linkBlk.UnlockLink();
                        }
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

        // --- SPECIAL CASE: GROUND DEPLOYER BLOCK ---
        if (block is GroundDeployerBlock deployerBlock)
        {
            if (deployerBlock.TryMoveAndDeploy(direction))
            {
                StartCoroutine(PostMoveRoutine());
            }
            else
            {
                block.PlayIllegalMoveAnimation(direction);
            }
            return;
        }

        // -- SPECIAL CASE: LINKER BLOCK ---
        if (block is LinkerBlock linkerBlock)
        {

            //We will Resolve it By Throwing The RayCAst In the Directio0n Of Movemnet
            Vector2Int targetPos = linkerBlock.gridPosition + direction;
            Debug.DrawLine(linkerBlock.currentIsland.GridToWorldPosition(linkerBlock.gridPosition), linkerBlock.currentIsland.GridToWorldPosition(targetPos), Color.red, 1f);
            // A. VALID IN-BOUNDS MOVE
            if (linkerBlock.currentIsland.CanMoveBlockLocal(linkerBlock.gridPosition, targetPos))
            {
                linkerBlock.currentIsland.ExecuteMoveLocal(linkerBlock.gridPosition, targetPos);
                linkerBlock.MoveToGridPosition(targetPos);
                if (linkerBlock.IsLocked)
                {
                    linkerBlock.UnlockLink();
                }
                StartCoroutine(PostMoveRoutine());
            }
            // B. OUT-OF-BOUNDS MOVE: Check for scale-mismatched island at edge
            else
            {
                List<GridIsland> mismatchedIslands = linkerBlock.DetectNearbyIslands(direction);

                if (mismatchedIslands.Count > 0)
                {
                    foreach (GridIsland island in mismatchedIslands)
                    {
                        Debug.Log($"[Scale Mismatch Link] Linked to Island ID: {island.islandID}");
                        linkerBlock.currentIsland.LinkToIsland(island, linkerBlock);
                        // LOCK THE LINKER BLOCK AND CHANGE COLOR
                        linkerBlock.LockLink(island);
                    }

                    StartCoroutine(PostMoveRoutine());
                }
                else
                {
                    block.PlayIllegalMoveAnimation(direction);
                }
            }
            return;
        }


        // --- STANDARD CASE: REGULAR BLOCK ---
        Vector2Int currentLocalPos = block.gridPosition;
        Vector2Int targetLocalPos = currentLocalPos + direction;

        if (sourceIsland.IsValidLocalPos(targetLocalPos))
        {
            if (sourceIsland.CanMoveBlockLocal(currentLocalPos, targetLocalPos)&& !block.IsBlockHighlightedAtTarget(targetLocalPos))
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
            // --- INTER-ISLAND TRANSFER CHECK WITH RAYCAST ---

            Vector2 dir2D = new Vector2(direction.x, direction.y).normalized;

            // 1. Calculate Edge using Renderer/Collider bounds
            Bounds blockBounds = block.GetComponent<Renderer>().bounds;
            float edgeExtent = (dir2D.x != 0) ? blockBounds.extents.x : blockBounds.extents.y;

            // Shift origin 0.05f outside the block's own collider to prevent self-collision
            Vector2 blockEdgeWorld = (Vector2)blockBounds.center + (dir2D * (edgeExtent + 0.05f));

            // 2. Define Raycast range and sample target point
            float checkDistance = Mathf.Max(sourceIsland.tileSize, 0.5f);
            Vector2 sampleTargetWorldPos = blockEdgeWorld + (dir2D * (checkDistance * 0.5f));

            // --- SCENE VIEW VISUALIZATION ---
            // Red ray shows the exact Raycast path in Scene view
            Debug.DrawRay(blockEdgeWorld, dir2D * checkDistance, Color.red, 2.0f);

            Debug.Log($"<color=cyan>[Transfer Check]</color> Initiating move for '{block.name}' in dir {direction}. " +
                      $"Source Island: '{sourceIsland.name}' (isLinked: {sourceIsland.isLinked})");
            Debug.Log($"[Transfer Check] Edge World Pos: {blockEdgeWorld} | Ray Distance: {checkDistance}");

            // 3. Detect Target Island via RaycastAll
            GridIsland targetIsland = null;
            RaycastHit2D[] hits = Physics2D.RaycastAll(blockEdgeWorld, dir2D, checkDistance);

            Debug.Log($"[Transfer Check] Raycast hit {hits.Length} colliders.");

            foreach (var hit in hits)
            {
                // Ignore self
                if (hit.collider == null || hit.collider.gameObject == block.gameObject)
                {
                    Debug.Log($"  -> Ignoring self collider on '{hit.collider?.name}'");
                    continue;
                }

                // Check parent GridIsland script
                GridIsland island = hit.collider.GetComponentInParent<GridIsland>();
                if (island != null)
                {
                    if (island == sourceIsland)
                    {
                        Debug.Log($"  -> Ignoring source island collider on '{hit.collider.name}'");
                        continue;
                    }

                    targetIsland = island;
                    Debug.Log($"<color=green>[Transfer Check] Detected Target Island:</color> '{targetIsland.name}' via collider '{hit.collider.name}'");
                    break;
                }

                // Fallback: Check if hit object has a Block attached to a different island
                Block hitBlock = hit.collider.GetComponent<Block>();
                if (hitBlock != null && hitBlock.currentIsland != null && hitBlock.currentIsland != sourceIsland)
                {
                    targetIsland = hitBlock.currentIsland;
                    Debug.Log($"<color=green>[Transfer Check] Detected Target Island via hit Block:</color> '{targetIsland.name}' (Block: '{hitBlock.name}')");
                    break;
                }
            }

            // 4. Attempt Transfer via MultiGridManager
            if (targetIsland != null && targetIsland != sourceIsland)
            {
                Debug.Log($"<color=yellow>[Transfer Check] Calling TryTransferBlockBetweenIslands...</color> Target Island: '{targetIsland.name}' (isLinked: {targetIsland.isLinked})");

                if (multiGridManager.TryTransferBlockBetweenIslands(block, sourceIsland, sampleTargetWorldPos, targetIsland))
                {
                    Debug.Log($"<color=lime>[Transfer Success]</color> Block '{block.name}' transferred to '{targetIsland.name}' at local grid {block.gridPosition}.");
                    block.MoveToGridPosition(block.gridPosition);
                    StartCoroutine(PostMoveRoutine());
                }
                else
                {
                    Debug.LogWarning($"<color=red>[Transfer Rejected]</color> TryTransferBlockBetweenIslands returned FALSE.");
                    block.PlayIllegalMoveAnimation(direction);
                }
            }
            else
            {
                Debug.LogWarning($"<color=orange>[Transfer Failed]</color> No valid target island detected ahead of block edge.");
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