using System.Collections.Generic;
using UnityEngine;

public class GridIsland : MonoBehaviour
{
    public int islandID;
    public int width;
    public int height;
    public Vector2Int originPosition;

    private GridCell[,] cellGrid;
    private Block[,] localGrid;
    private List<Block> islandBlocks = new List<Block>();

    public void InitializeIsland(int id, int w, int h, Vector2Int origin, GridCell[,] initialCells)
    {
        islandID = id;
        width = w;
        height = h;
        originPosition = origin;

        cellGrid = new GridCell[width, height];
        localGrid = new Block[width, height];
        islandBlocks.Clear();

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                cellGrid[x, y] = initialCells != null ? initialCells[x, y] : new GridCell(TileType.Empty);
            }
        }
    }

    /// <summary>
    /// Coordinates are valid ONLY if inside array bounds AND the cell type is not Empty.
    /// </summary>
    public bool IsValidLocalPos(Vector2Int pos)
    {
        bool inBounds = pos.x >= 0 && pos.x < width && pos.y >= 0 && pos.y < height;
        if (!inBounds) return false;

        return cellGrid[pos.x, pos.y].IsActive;
    }

    public Vector2Int WorldToLocal(Vector2Int worldPos) => worldPos - originPosition;

    public bool ContainsWorldPos(Vector2Int worldPos) => IsValidLocalPos(WorldToLocal(worldPos));

    public void RegisterBlock(Block block, Vector2Int localPos)
    {
        if (IsValidLocalPos(localPos))
        {
            localGrid[localPos.x, localPos.y] = block;
            if (!islandBlocks.Contains(block)) islandBlocks.Add(block);
            block.currentIsland = this;
        }
    }

    public Block GetBlockAtLocalPos(Vector2Int localPos)
    {
        return IsValidLocalPos(localPos) ? localGrid[localPos.x, localPos.y] : null;
    }

    public bool IsCellOccupiedLocal(Vector2Int localPos)
    {
        if (!IsValidLocalPos(localPos)) return true; // Treat void space as solid boundary
        return localGrid[localPos.x, localPos.y] != null;
    }

    public bool CanMoveBlockLocal(Vector2Int from, Vector2Int to)
    {
        if (!IsValidLocalPos(to)) return false;
        if (IsCellOccupiedLocal(to)) return false;
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

    // --- RESTORED INTER-ISLAND TRANSFER METHODS ---

    public bool CanRemoveBlock(Vector2Int localPos)
    {
        Block movingBlock = GetBlockAtLocalPos(localPos);
        if (movingBlock == null) return false;

        localGrid[localPos.x, localPos.y] = null;
        islandBlocks.Remove(movingBlock);

        bool isConnected = VerifyConnectivity();

        localGrid[localPos.x, localPos.y] = movingBlock;
        islandBlocks.Add(movingBlock);

        return isConnected;
    }

    public void RemoveBlock(Vector2Int localPos)
    {
        Block block = GetBlockAtLocalPos(localPos);
        if (block != null)
        {
            localGrid[localPos.x, localPos.y] = null;
            islandBlocks.Remove(block);
            block.currentIsland = null;
        }
    }

    public void ReceiveBlock(Block block, Vector2Int localPos)
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
}