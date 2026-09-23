using System.Collections.Generic;
using UnityEngine;

public class JointBlock : Block
{
    [Header("Multi-Selection Settings")]
    [SerializeField] private List<ImmovableBlock> linkedImmovables = new List<ImmovableBlock>();
    private List<ImmovableBlock> adjacentImmovables = new List<ImmovableBlock>();
    private bool isSelectionActive = false;

    public bool IsSelectionActive => isSelectionActive;
    public List<ImmovableBlock> LinkedImmovables => linkedImmovables;

    public override bool CanPlayerMoveDirectly() => true;
    public override bool IsAffectedByGravity() => false;

    /// <summary>
    /// Finds all orthogonally adjacent Immovable Blocks.
    /// </summary>
    public List<ImmovableBlock> GetAdjacentImmovableBlocks()
    {
        adjacentImmovables.Clear();
        if (currentIsland == null) return adjacentImmovables;

        Vector2Int[] adjacentDirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        foreach (var dir in adjacentDirs)
        {
            Vector2Int neighborPos = gridPosition + dir;
            if (currentIsland.IsValidLocalPos(neighborPos))
            {
                Block neighbor = currentIsland.GetBlockAtLocalPos(neighborPos);
                if (neighbor is ImmovableBlock immovable)
                {
                    adjacentImmovables.Add(immovable);
                }
            }
        }

        return adjacentImmovables;
    }

    /// <summary>
    /// Toggles linking a specific adjacent Immovable Block.
    /// </summary>
    public void ToggleLinkImmovableBlock(ImmovableBlock target)
    {
        List<ImmovableBlock> validNeighbors = GetAdjacentImmovableBlocks();
        if (!validNeighbors.Contains(target)) return;

        if (linkedImmovables.Contains(target))
        {
            // Unlink target
            target.SetLinkedHighlight(false);
            linkedImmovables.Remove(target);
        }
        else
        {
            // Link target
            target.SetLinkedHighlight(true);
            linkedImmovables.Add(target);
        }

        isSelectionActive = linkedImmovables.Count > 0;
        SetJointHighlight(isSelectionActive);
    }

    /// <summary>
    /// Clears all linked immovable blocks and resets highlights.
    /// </summary>
    public void DeselectAll()
    {
        isSelectionActive = false;

        foreach (var immovable in linkedImmovables)
        {
            if (immovable != null)
            {
                immovable.SetLinkedHighlight(false);
                immovable.SetBlinkingState(false);
            }
        }

        linkedImmovables.Clear();
        SetJointHighlight(false);
    }

    public void SetJointHighlight(bool enable)
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.color = enable ? Color.yellow : Color.white;
        }
    }

    /// <summary>
    /// Moves the JointBlock and ALL linked ImmovableBlocks together in the swipe direction.
    /// </summary>
    public bool TryMoveWithSelectedImmovables(Vector2Int direction)
    {
        if (currentIsland == null) return false;

        GridIsland savedIsland = currentIsland;
        Vector2Int jointCurrentPos = gridPosition;
        Vector2Int jointTargetPos = jointCurrentPos + direction;

        // --- CASE 1: MULTIPLE OR SINGLE LINKED IMMOVABLE BLOCKS ---
        if (linkedImmovables.Count > 0)
        {
            // Collect all blocks in the multi-group (Joint + Linked Immovables)
            List<Block> group = new List<Block>();
            group.Add(this);

            foreach (var immovable in linkedImmovables)
            {
                if (immovable != null && !group.Contains(immovable))
                {
                    group.Add(immovable);
                }
            }

            // 1. Check movement validity for EVERY block in the group
            foreach (Block b in group)
            {
                GridIsland blockIsland = b.currentIsland != null ? b.currentIsland : savedIsland;
                Vector2Int targetPos = b.gridPosition + direction;

                if (!blockIsland.IsValidLocalPos(targetPos))
                {
                    return false; // Out of island bounds
                }

                // A cell is valid if it is empty OR occupied by a block that is ALSO moving as part of this group
                if (blockIsland.IsCellOccupiedLocal(targetPos))
                {
                    Block occupant = blockIsland.GetBlockAtLocalPos(targetPos);
                    if (!group.Contains(occupant))
                    {
                        return false; // Blocked by an unlinked obstacle
                    }
                }
            }

            // 2. Clear all group block slots from grid temporarily to avoid self-collision
            foreach (Block b in group)
            {
                GridIsland island = b.currentIsland != null ? b.currentIsland : savedIsland;
                island.RemoveBlock(b.gridPosition);
            }

            // 3. Register all blocks at their new grid positions
            foreach (Block b in group)
            {
                GridIsland island = b.currentIsland != null ? b.currentIsland : savedIsland;
                Vector2Int newPos = b.gridPosition + direction;

                island.RegisterBlock(b, newPos);
                b.gridPosition = newPos;
                b.MoveToGridPosition(newPos);
            }

            // 4. Verify that each linked immovable block is still orthogonally adjacent to the joint
            List<ImmovableBlock> disconnected = new List<ImmovableBlock>();
            foreach (var immovable in linkedImmovables)
            {
                int distance = Mathf.Abs(this.gridPosition.x - immovable.gridPosition.x) +
                               Mathf.Abs(this.gridPosition.y - immovable.gridPosition.y);

                if (distance != 1)
                {
                    disconnected.Add(immovable);
                }
            }

            // Clean up any blocks that were separated
            foreach (var disc in disconnected)
            {
                disc.SetLinkedHighlight(false);
                linkedImmovables.Remove(disc);
            }

            if (linkedImmovables.Count == 0)
            {
                DeselectAll();
            }

            return true;
        }

        // --- CASE 2: STANDALONE JOINT MOVE ---
        if (savedIsland.CanMoveBlockLocal(jointCurrentPos, jointTargetPos))
        {
            savedIsland.ExecuteMoveLocal(jointCurrentPos, jointTargetPos);
            this.gridPosition = jointTargetPos;
            this.MoveToGridPosition(jointTargetPos);
            return true;
        }

        return false;
    }
}