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

    public bool TryTransferBlockBetweenIslands(Block block, GridIsland sourceIsland, Vector3 sampleTargetWorldPos, GridIsland targetIsland)
    {
        if (targetIsland == null)
        {
            Debug.LogError("[Manager Fail] Target island is NULL.");
            return false;
        }

        if (targetIsland == sourceIsland)
        {
            Debug.LogError("[Manager Fail] Target island is the same as Source island.");
            return false;
        }

        // --- 1. LINK STATE VALIDATION ---
        if (!targetIsland.isLinked)
        {
            Debug.LogWarning($"[Manager Fail] Target Island '{targetIsland.name}' has 'isLinked = false'. Transfer blocked!");
            return false;
        }

        // Convert sample world point to target local grid
        Vector2Int targetLocalPos = targetIsland.WorldToGridPosition(sampleTargetWorldPos);
        Debug.Log($"[Manager Step] Converted sample world pos {sampleTargetWorldPos} -> Target local grid pos: {targetLocalPos} on '{targetIsland.name}'");

        // --- 2. BOUNDS VALIDATION ---
        if (!targetIsland.IsValidLocalPos(targetLocalPos))
        {
            Debug.LogWarning($"[Manager Fail] Target local pos {targetLocalPos} is OUT OF BOUNDS on island '{targetIsland.name}'.");
            return false;
        }

        // --- 3. OCCUPANCY VALIDATION ---
        if (targetIsland.IsCellOccupiedLocal(targetLocalPos))
        {
            Block occupant = targetIsland.GetBlockAtLocalPos(targetLocalPos);
            string occupantName = occupant != null ? occupant.name : "Unknown";
            Debug.LogWarning($"[Manager Fail] Cell {targetLocalPos} on island '{targetIsland.name}' is OCCUPIED by '{occupantName}'.");
            return false;
        }

       

        // --- EXECUTE TRANSFER ---
        Debug.Log($"<color=cyan>[Manager Executing Transfer]</color> Moving '{block.name}' from '{sourceIsland.name}' ({block.gridPosition}) to '{targetIsland.name}' ({targetLocalPos})...");

        sourceIsland.RemoveBlock(block.gridPosition);

        // Reparent and preserve world entry position
        block.transform.SetParent(targetIsland.transform, true);
        block.currentIsland = targetIsland;

        // Adjust local scale
        Vector3 targetBoxScale = (targetIsland.boxScale != Vector2.zero) ? (Vector3)targetIsland.boxScale : Vector3.one;
        block.transform.localScale = new Vector3(
            Mathf.Abs(targetBoxScale.x),
            Mathf.Abs(targetBoxScale.y),
            1f
        );

        targetIsland.ReceiveBlock(block, targetLocalPos);
        block.gridPosition = targetLocalPos;

        return true;
    }
}