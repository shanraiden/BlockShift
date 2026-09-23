using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.RuleTile.TilingRuleOutput;

public partial class GridIsland : MonoBehaviour
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

public partial class GridIsland : MonoBehaviour
{
    public void ExpandGridIfNeeded(Vector2Int targetLocalPos)
    {
        int requiredWidth = Mathf.Max(width, targetLocalPos.x + 1);
        int requiredHeight = Mathf.Max(height, targetLocalPos.y + 1);

        int offsetX = targetLocalPos.x < 0 ? -targetLocalPos.x : 0;
        int offsetY = targetLocalPos.y < 0 ? -targetLocalPos.y : 0;

        if (requiredWidth <= width && requiredHeight <= height && offsetX == 0 && offsetY == 0)
        {
            return; // Array is already large enough
        }

        int newWidth = requiredWidth + offsetX;
        int newHeight = requiredHeight + offsetY;

        Block[,] newLocalGrid = new Block[newWidth, newHeight];
        GridCell[,] newCellGrid = new GridCell[newWidth, newHeight];

        // 1. Fill new cell grid with empty tiles
        for (int x = 0; x < newWidth; x++)
        {
            for (int y = 0; y < newHeight; y++)
            {
                newCellGrid[x, y] = new GridCell(TileType.Empty);
            }
        }

        // 2. Copy existing cellGrid and localGrid
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (cellGrid != null)
                {
                    newCellGrid[x + offsetX, y + offsetY] = cellGrid[x, y];
                }

                Block block = localGrid[x, y];
                if (block != null)
                {
                    newLocalGrid[x + offsetX, y + offsetY] = block;
                    block.gridPosition += new Vector2Int(offsetX, offsetY);
                }
            }
        }

        // 3. Handle negative coordinate expansion (-X or -Y)
        if (offsetX > 0 || offsetY > 0)
        {
            originPosition -= new Vector2Int(offsetX, offsetY);

            // Move the parent island root left/down in world space
            transform.position -= new Vector3(offsetX, offsetY, 0);

            // Shift ALL child transforms (ground tiles, dot tiles, blocks) right/up in local space
            // World position = (IslandPos - offset) + (LocalPos + offset) = Unchanged!
            foreach (UnityEngine.Transform child in transform)
            {
                child.localPosition += new Vector3(offsetX, offsetY, 0);
            }
        }

        localGrid = newLocalGrid;
        cellGrid = newCellGrid;
        width = newWidth;
        height = newHeight;
    }

    /// <summary>
    /// Spawns a background ground tile without double-expanding.
    /// </summary>
    public void DeployGroundTileAt(Vector2Int localPos, GameObject groundPrefab)
    {
        if (cellGrid != null && localPos.x >= 0 && localPos.x < width && localPos.y >= 0 && localPos.y < height)
        {
            cellGrid[localPos.x, localPos.y] = new GridCell(TileType.Ground);
        }

        if (groundPrefab != null)
        {
            GameObject bgGO = Instantiate(groundPrefab, transform);
            bgGO.transform.localPosition = new Vector3(localPos.x, localPos.y, 0.1f);
            bgGO.transform.localRotation = Quaternion.identity;
            bgGO.transform.localScale = Vector3.one;
        }
    }
}

public partial class GridIsland : MonoBehaviour
{
    public Vector2Int WorldToLocalPos(Vector3 worldPos)
    {
        Vector3 localPos = transform.InverseTransformPoint(worldPos);
        return new Vector2Int(Mathf.RoundToInt(localPos.x), Mathf.RoundToInt(localPos.y));
    }

    /// <summary>
    /// Self-contained island absorption. Preserves all block positions and grid references 
    /// without modifying existing external methods like RegisterBlock.
    /// </summary>
    public void MergeOtherIsland(GridIsland otherIsland)
    {
        if (otherIsland == null || otherIsland == this) return;

        Debug.Log($"<color=yellow>[GridIsland] Starting Merge: Absorption of '{otherIsland.name}' into '{this.name}'</color>");

        // 1. Gather all Block components from Island 2 and cache their exact World Positions
        Block[] foundBlocks = otherIsland.GetComponentsInChildren<Block>();
        List<Block> blocksToTransfer = new List<Block>(foundBlocks);
        List<Vector3> blockWorldPositions = new List<Vector3>();

        for (int i = 0; i < blocksToTransfer.Count; i++)
        {
            if (blocksToTransfer[i] != null)
            {
                blockWorldPositions.Add(blocksToTransfer[i].transform.position);
            }
        }

        // 2. Gather non-Block visual transforms (ground tiles, decorations) and cache World Positions
        List<UnityEngine.Transform> visualChildren = new List<UnityEngine.Transform>();
        List<Vector3> visualWorldPositions = new List<Vector3>();

        foreach (UnityEngine.Transform child in otherIsland.transform)
        {
            if (child.GetComponent<Block>() == null && child.GetComponentInChildren<Block>() == null)
            {
                visualChildren.Add(child);
                visualWorldPositions.Add(child.position);
            }
        }

        // 3. STEP A: PRE-EXPAND Island 1 grid to fit ALL incoming block & visual positions.
        // Doing this before moving anything keeps Island 1's local coordinate origin locked!
        for (int i = 0; i < blockWorldPositions.Count; i++)
        {
            Vector2Int targetLocalPos = WorldToLocalPos(blockWorldPositions[i]);
            ExpandGridIfNeeded(targetLocalPos);
        }

        for (int i = 0; i < visualWorldPositions.Count; i++)
        {
            Vector2Int targetLocalPos = WorldToLocalPos(visualWorldPositions[i]);
            ExpandGridIfNeeded(targetLocalPos);
        }

        // 4. STEP B: TRANSFER AND REGISTER ALL BLOCKS
        for (int i = 0; i < blocksToTransfer.Count; i++)
        {
            Block block = blocksToTransfer[i];
            if (block == null) continue;

            // Remove from old island
            otherIsland.RemoveBlock(block.gridPosition);

            // Re-calculate local grid coordinate on Island 1 now that expansion is locked
            Vector2Int newLocalPos = WorldToLocalPos(blockWorldPositions[i]);

            // Double check array limits after expansion
            if (newLocalPos.x >= 0 && newLocalPos.x < width && newLocalPos.y >= 0 && newLocalPos.y < height)
            {
                // Check if target cell is already occupied by an Island 1 block
                if (localGrid[newLocalPos.x, newLocalPos.y] != null && localGrid[newLocalPos.x, newLocalPos.y] != block)
                {
                    Debug.LogWarning($"[GridIsland] Collision at {newLocalPos} for '{block.name}'. Finding free cell...");
                    newLocalPos = FindNearestFreeCell(newLocalPos);
                    ExpandGridIfNeeded(newLocalPos);
                }
            }

            // Update transform parenting & island reference
            block.transform.SetParent(this.transform);
            block.currentIsland = this;
            block.gridPosition = newLocalPos;

            // Direct internal registration (No modified methods called)
            if (!islandBlocks.Contains(block))
            {
                islandBlocks.Add(block);
            }

            if (newLocalPos.x >= 0 && newLocalPos.x < width && newLocalPos.y >= 0 && newLocalPos.y < height)
            {
                localGrid[newLocalPos.x, newLocalPos.y] = block;

                if (cellGrid != null)
                {
                    cellGrid[newLocalPos.x, newLocalPos.y] = new GridCell(TileType.Ground);
                }
            }

            // Snap physical position to match grid index
            block.MoveToGridPosition(newLocalPos);

            Debug.Log($"[GridIsland] Transferred '{block.name}' -> Island1 Local Pos: {newLocalPos}");
        }

        // 5. STEP C: TRANSFER VISUAL TILES
        for (int i = 0; i < visualChildren.Count; i++)
        {
            UnityEngine.Transform visual = visualChildren[i];
            if (visual == null) continue;

            Vector2Int targetLocalPos = WorldToLocalPos(visualWorldPositions[i]);

            visual.SetParent(this.transform);
            visual.localPosition = new Vector3(targetLocalPos.x, targetLocalPos.y, visual.localPosition.z);

            if (cellGrid != null && targetLocalPos.x >= 0 && targetLocalPos.x < width && targetLocalPos.y >= 0 && targetLocalPos.y < height)
            {
                cellGrid[targetLocalPos.x, targetLocalPos.y] = new GridCell(TileType.Ground);
            }
        }

        // 6. Clean up secondary island GameObject
        Debug.Log($"<color=cyan>[GridIsland] Destroying old island '{otherIsland.name}'</color>");
        Destroy(otherIsland.gameObject);
    }

    private Vector2Int FindNearestFreeCell(Vector2Int startPos)
    {
        Vector2Int[] offsets = new Vector2Int[]
        {
            Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right,
            new Vector2Int(1, 1), new Vector2Int(-1, 1), new Vector2Int(1, -1), new Vector2Int(-1, -1)
        };

        foreach (Vector2Int dir in offsets)
        {
            Vector2Int candidate = startPos + dir;
            if (candidate.x >= 0 && candidate.x < width && candidate.y >= 0 && candidate.y < height)
            {
                if (localGrid[candidate.x, candidate.y] == null)
                {
                    return candidate;
                }
            }
        }

        return startPos;
    }

   
}