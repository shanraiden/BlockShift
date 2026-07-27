using UnityEngine;

public class JointBlock : Block
{
    public override bool CanPlayerMoveDirectly() => true;
    public override bool IsAffectedByGravity() => false;

    /// <summary>
    /// Checks if this Joint Block is orthogonally adjacent to an Immovable Block.
    /// </summary>
    public bool IsTouchingImmovableBlock(out ImmovableBlock attachedAnchor)
    {
        attachedAnchor = null;
        if (currentIsland == null) return false;

        Vector2Int[] adjacentDirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        foreach (var dir in adjacentDirs)
        {
            Vector2Int neighborPos = gridPosition + dir;
            if (currentIsland.IsValidLocalPos(neighborPos))
            {
                Block neighbor = currentIsland.GetBlockAtLocalPos(neighborPos);
                if (neighbor is ImmovableBlock immovable)
                {
                    attachedAnchor = immovable;
                    return true;
                }
            }
        }

        return false;
    }
}