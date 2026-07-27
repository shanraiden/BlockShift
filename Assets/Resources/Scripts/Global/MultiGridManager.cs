using System.Collections.Generic;
using UnityEngine;

public class MultiGridManager : MonoBehaviour
{
    private List<GridIsland> activeIslands = new List<GridIsland>();

    public void RegisterIsland(GridIsland island)
    {
        if (!activeIslands.Contains(island))
            activeIslands.Add(island);
    }
    public List<GridIsland> GetActiveIslands()
    {
        return activeIslands;
    }
    public void ClearIslands()
    {
        activeIslands.Clear();
    }

    // Finds which island contains the specified World Grid position
    public GridIsland GetIslandAtWorldPos(Vector2Int worldPos)
    {
        foreach (var island in activeIslands)
        {
            if (island.ContainsWorldPos(worldPos))
            {
                return island;
            }
        }
        return null;
    }

    // Handles transferring a block from source island to target island
    public bool TryTransferBlockBetweenIslands(Block block, GridIsland sourceIsland, Vector2Int targetWorldPos)
    {
        GridIsland targetIsland = GetIslandAtWorldPos(targetWorldPos);

        // No island exists at target world position
        if (targetIsland == null) return false;

        Vector2Int targetLocalPos = targetIsland.WorldToLocal(targetWorldPos);

        // Target cell must be empty
        if (targetIsland.IsCellOccupiedLocal(targetLocalPos)) return false;

        // Source island must remain fully connected after losing this block
        if (!sourceIsland.CanRemoveBlock(block.gridPosition)) return false;

        // Execute Transfer
        sourceIsland.RemoveBlock(block.gridPosition);
        targetIsland.ReceiveBlock(block, targetLocalPos);

        return true;
    }
}