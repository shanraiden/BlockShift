using System.Collections.Generic;
using UnityEngine;

public class GridIsland : MonoBehaviour
{
    public int islandID;
    public int width;
    public int height;
    public Vector2Int originPosition; // World grid offset

    private Block[,] localGrid;
    private List<Block> islandBlocks = new List<Block>();

    public void InitializeIsland(int id, int w, int h, Vector2Int origin)
    {
        islandID = id;
        width = w;
        height = h;
        originPosition = origin;
        localGrid = new Block[width, height];
        islandBlocks.Clear();
    }

    // Converts World Grid coordinates to Local Island coordinates
    public Vector2Int WorldToLocal(Vector2Int worldPos)
    {
        return worldPos - originPosition;
    }

    // Checks if World coordinates fall inside this island's grid boundaries
    public bool ContainsWorldPos(Vector2Int worldPos)
    {
        Vector2Int local = WorldToLocal(worldPos);
        return IsValidLocalPos(local);
    }
    public void RegisterBlock(Block block, Vector2Int localPos)
    {
        if (IsValidLocalPos(localPos))
        {
            localGrid[localPos.x, localPos.y] = block;
            if (!islandBlocks.Contains(block))
            {
                islandBlocks.Add(block);
            }
            block.currentIsland = this;
        }
    }

    /// <summary>
    /// Validates whether a primary block and an attached secondary block can move together in 'direction'
    /// without hitting obstacles or breaking the rest of the island's cluster connectivity.
    /// </summary>
    public bool CanMoveBlockPairLocal(Block primary, Block secondary, Vector2Int direction)
    {
        Vector2Int primaryFrom = primary.gridPosition;
        Vector2Int primaryTo = primaryFrom + direction;

        Vector2Int secondaryFrom = secondary.gridPosition;
        Vector2Int secondaryTo = secondaryFrom + direction;

        // 1. Check bounds for both destination cells
        if (!IsValidLocalPos(primaryTo) || !IsValidLocalPos(secondaryTo))
            return false;

        // 2. Check destination occupancy (destination cells must be empty, ignoring each other's starting cells)
        Block primaryDestBlock = localGrid[primaryTo.x, primaryTo.y];
        if (primaryDestBlock != null && primaryDestBlock != secondary)
            return false;

        Block secondaryDestBlock = localGrid[secondaryTo.x, secondaryTo.y];
        if (secondaryDestBlock != null && secondaryDestBlock != primary)
            return false;

        // 3. Simulate moving BOTH blocks simultaneously
        localGrid[primaryFrom.x, primaryFrom.y] = null;
        localGrid[secondaryFrom.x, secondaryFrom.y] = null;

        localGrid[primaryTo.x, primaryTo.y] = primary;
        localGrid[secondaryTo.x, secondaryTo.y] = secondary;

        // 4. Verify overall connectivity of the island with both blocks in new positions
        bool isConnected = VerifyConnectivity();

        // 5. Revert simulated state
        localGrid[primaryTo.x, primaryTo.y] = null;
        localGrid[secondaryTo.x, secondaryTo.y] = null;

        localGrid[primaryFrom.x, primaryFrom.y] = primary;
        localGrid[secondaryFrom.x, secondaryFrom.y] = secondary;

        return isConnected;
    }

    /// <summary>
    /// Executes a synchronized move for a pair of attached blocks.
    /// </summary>
    public void ExecuteMoveBlockPairLocal(Block primary, Block secondary, Vector2Int direction)
    {
        Vector2Int primaryFrom = primary.gridPosition;
        Vector2Int primaryTo = primaryFrom + direction;

        Vector2Int secondaryFrom = secondary.gridPosition;
        Vector2Int secondaryTo = secondaryFrom + direction;

        // Clear old cells
        localGrid[primaryFrom.x, primaryFrom.y] = null;
        localGrid[secondaryFrom.x, secondaryFrom.y] = null;

        // Set new cells
        localGrid[primaryTo.x, primaryTo.y] = primary;
        localGrid[secondaryTo.x, secondaryTo.y] = secondary;

        // Animate both blocks
        primary.MoveToGridPosition(primaryTo);
        secondary.MoveToGridPosition(secondaryTo);
    }

    public Block GetBlockAtLocalPos(Vector2Int localPos)
    {
        if (IsValidLocalPos(localPos))
        {
            return localGrid[localPos.x, localPos.y];
        }
        return null;
    }

    public bool IsCellOccupiedLocal(Vector2Int localPos)
    {
        if (!IsValidLocalPos(localPos)) return true; // Treat out-of-bounds as occupied
        return localGrid[localPos.x, localPos.y] != null;
    }

    // Validates if removing a block leaves the source island connected
    public bool CanRemoveBlock(Vector2Int localPos)
    {
        Block movingBlock = localGrid[localPos.x, localPos.y];
        if (movingBlock == null) return false;

        // Temporarily remove
        localGrid[localPos.x, localPos.y] = null;
        islandBlocks.Remove(movingBlock);

        bool isConnected = VerifyConnectivity();

        // Revert temporary removal
        localGrid[localPos.x, localPos.y] = movingBlock;
        islandBlocks.Add(movingBlock);

        return isConnected;
    }

    // Removes block from source island permanently
    public void RemoveBlock(Vector2Int localPos)
    {
        Block block = localGrid[localPos.x, localPos.y];
        if (block != null)
        {
            localGrid[localPos.x, localPos.y] = null;
            islandBlocks.Remove(block);
            block.currentIsland = null;
        }
    }

    // Adds block to target island
    public void ReceiveBlock(Block block, Vector2Int localPos)
    {
        localGrid[localPos.x, localPos.y] = block;
        islandBlocks.Add(block);
        block.currentIsland = this;
    }

    /// <summary>
    /// Checks whether a single block can move to an adjacent local position.
    /// </summary>
    public bool CanMoveBlockLocal(Vector2Int from, Vector2Int to)
    {
        // 1. Target cell must be within grid bounds
        if (!IsValidLocalPos(to))
            return false;

        // 2. Target cell must NOT be occupied by another block
        if (IsCellOccupiedLocal(to))
            return false;

        // Optional: If your game REQUIRES all blocks on this island to remain connected as a single cluster,
        // uncomment the simulation check below. If you want free movement, keep it commented out.
        /*
        Block blockToMove = localGrid[from.x, from.y];
        localGrid[from.x, from.y] = null;
        localGrid[to.x, to.y] = blockToMove;

        bool remainsConnected = VerifyConnectivity();

        // Revert simulation
        localGrid[to.x, to.y] = null;
        localGrid[from.x, from.y] = blockToMove;

        if (!remainsConnected) return false;
        */

        return true;
    }

    public void ExecuteMoveLocal(Vector2Int from, Vector2Int to)
    {
        Block block = localGrid[from.x, from.y];
        if (block == null) return;

        localGrid[from.x, from.y] = null;
        localGrid[to.x, to.y] = block;
        block.gridPosition = to;
    }

    private bool VerifyConnectivity()
    {
        if (islandBlocks.Count <= 1) return true;

        HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
        Queue<Vector2Int> queue = new Queue<Vector2Int>();

        Vector2Int start = GetFirstBlockLocalPos();
        if (start.x == -1) return false;

        queue.Enqueue(start);
        visited.Add(start);

        Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();

            foreach (var dir in directions)
            {
                Vector2Int neighbor = current + dir;

                if (IsValidLocalPos(neighbor) && localGrid[neighbor.x, neighbor.y] != null)
                {
                    if (!visited.Contains(neighbor))
                    {
                        visited.Add(neighbor);
                        queue.Enqueue(neighbor);
                    }
                }
            }
        }

        return visited.Count == islandBlocks.Count;
    }

    private Vector2Int GetFirstBlockLocalPos()
    {
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                if (localGrid[x, y] != null) return new Vector2Int(x, y);

        return new Vector2Int(-1, -1);
    }

    public bool IsValidLocalPos(Vector2Int pos)
    {
        return pos.x >= 0 && pos.x < width && pos.y >= 0 && pos.y < height;
    }
}