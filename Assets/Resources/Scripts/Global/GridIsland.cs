using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class GridIsland : MonoBehaviour
{
    public int islandID;
    public int width;
    public int height;
    public Vector2Int originPosition;

    [Header("Cell Scaling")]
    public Vector2 boxScale = Vector2.one; // Individual cell width and height multiplier

    private GridCell[,] cellGrid;
    private Block[,] localGrid;
    private List<Block> islandBlocks = new List<Block>();
    public int scaleFactorX;
    public int scaleFactorY;

    public void InitializeIsland(int id, int w, int h, Vector2Int origin, GridCell[,] initialCells, Vector2 customBoxScale = default)
    {
        islandID = id;
        width = w;
        height = h;
        originPosition = origin;
        boxScale = customBoxScale != Vector2.zero ? customBoxScale : Vector2.one;
        scaleFactorX = Mathf.Max(1, Mathf.RoundToInt(customBoxScale.x));
        scaleFactorY = Mathf.Max(1, Mathf.RoundToInt(customBoxScale.y));
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
    /// Checks if this island's scale matches another island's scale.
    /// </summary>
    public bool HasSameScale(GridIsland otherIsland)
    {
        if (otherIsland == null) return false;

        bool sameBoxScale = Mathf.Approximately(this.boxScale.x, otherIsland.boxScale.x) &&
                           Mathf.Approximately(this.boxScale.y, otherIsland.boxScale.y);

        bool sameScaleFactors = (this.scaleFactorX == otherIsland.scaleFactorX) &&
                                (this.scaleFactorY == otherIsland.scaleFactorY);

        return sameBoxScale && sameScaleFactors;
    }

    public Vector3 GridToLocalPosition(Vector2Int gridPos, float zOffset = 0f)
    {
        return new Vector3(gridPos.x * boxScale.x, gridPos.y * boxScale.y, zOffset);
    }

    public Vector2Int WorldToGridPosition(Vector3 worldPos)
    {
        Vector3 localPos = transform.InverseTransformPoint(worldPos);
        int x = Mathf.RoundToInt(localPos.x / Mathf.Max(0.001f, boxScale.x));
        int y = Mathf.RoundToInt(localPos.y / Mathf.Max(0.001f, boxScale.y));
        return new Vector2Int(x, y);
    }

    public Vector2Int WorldToLocalPos(Vector3 worldPos) => WorldToGridPosition(worldPos);

    public Vector2Int WorldToLocal(Vector2Int worldPos) => worldPos - originPosition;

    public bool ContainsWorldPos(Vector2Int worldPos) => IsValidLocalPos(WorldToLocal(worldPos));

    public bool IsValidLocalPos(Vector2Int pos)
    {
        bool inBounds = pos.x >= 0 && pos.x < width && pos.y >= 0 && pos.y < height;
        if (!inBounds) return false;

        return cellGrid != null && cellGrid[pos.x, pos.y].IsActive;
    }

    public void ExpandGridIfNeeded(Vector2Int targetLocalPos)
    {
        int offsetX = targetLocalPos.x < 0 ? -targetLocalPos.x : 0;
        int offsetY = targetLocalPos.y < 0 ? -targetLocalPos.y : 0;

        int requiredWidth = Mathf.Max(width + offsetX, targetLocalPos.x + 1 + offsetX);
        int requiredHeight = Mathf.Max(height + offsetY, targetLocalPos.y + 1 + offsetY);

        if (offsetX == 0 && offsetY == 0 && requiredWidth <= width && requiredHeight <= height)
        {
            return;
        }

        int newWidth = requiredWidth;
        int newHeight = requiredHeight;

        Block[,] newLocalGrid = new Block[newWidth, newHeight];
        GridCell[,] newCellGrid = new GridCell[newWidth, newHeight];

        for (int x = 0; x < newWidth; x++)
        {
            for (int y = 0; y < newHeight; y++)
            {
                newCellGrid[x, y] = new GridCell(TileType.Empty);
            }
        }

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
                    block.gridPosition = new Vector2Int(x + offsetX, y + offsetY);
                }
            }
        }

        localGrid = newLocalGrid;
        cellGrid = newCellGrid;
        width = newWidth;
        height = newHeight;

        if (offsetX > 0 || offsetY > 0)
        {
            originPosition -= new Vector2Int(offsetX, offsetY);

            Vector3 localShiftVector = new Vector3(offsetX * boxScale.x, offsetY * boxScale.y, 0f);

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    Block b = localGrid[x, y];
                    if (b != null)
                    {
                        b.transform.localPosition = GridToLocalPosition(b.gridPosition, b.transform.localPosition.z);
                    }
                }
            }

            foreach (Transform child in transform)
            {
                if (child.GetComponent<Block>() == null)
                {
                    child.localPosition += localShiftVector;
                }
            }

            transform.position -= transform.TransformVector(localShiftVector);
        }
    }

    public void DeployGroundTileAt(Vector2Int localPos, GameObject groundPrefab)
    {
        if (cellGrid != null && localPos.x >= 0 && localPos.x < width && localPos.y >= 0 && localPos.y < height)
        {
            cellGrid[localPos.x, localPos.y] = new GridCell(TileType.Ground);
        }

        if (groundPrefab != null)
        {
            GameObject bgGO = Instantiate(groundPrefab, transform);
            bgGO.transform.localPosition = GridToLocalPosition(localPos, 0.1f);
            bgGO.transform.localRotation = Quaternion.identity;
            bgGO.transform.localScale = new Vector3(boxScale.x, boxScale.y, 1f);
        }
    }

    public void RegisterBlock(Block block, Vector2Int localPos)
    {
        if (localPos.x >= 0 && localPos.x < width && localPos.y >= 0 && localPos.y < height)
        {
            localGrid[localPos.x, localPos.y] = block;
            if (!islandBlocks.Contains(block)) islandBlocks.Add(block);
            block.currentIsland = this;
        }
    }

    public void ReceiveBlock(Block block, Vector2Int localPos)
    {
        RegisterBlock(block, localPos);
    }

    public Block GetBlockAtLocalPos(Vector2Int localPos)
    {
        if (localPos.x >= 0 && localPos.x < width && localPos.y >= 0 && localPos.y < height)
        {
            return localGrid[localPos.x, localPos.y];
        }
        return null;
    }

    public bool IsCellOccupiedLocal(Vector2Int localPos)
    {
        if (!IsValidLocalPos(localPos)) return true;
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
        Block block = GetBlockAtLocalPos(from);
        if (block == null) return;

        localGrid[from.x, from.y] = null;
        localGrid[to.x, to.y] = block;
        block.gridPosition = to;
    }

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

    public void MergeOtherIsland(GridIsland otherIsland)
    {
        if (otherIsland == null || otherIsland == this) return;

        // CANCEL MERGE IF SCALES DO NOT MATCH
        if (!HasSameScale(otherIsland))
        {
            Debug.LogWarning($"Merge aborted: Scale mismatch between Island {this.islandID} and Island {otherIsland.islandID}");
            return;
        }

        // Collect cell types and world positions
        List<KeyValuePair<Vector3, TileType>> cellsToTransfer = new List<KeyValuePair<Vector3, TileType>>();
        if (otherIsland.cellGrid != null)
        {
            for (int x = 0; x < otherIsland.width; x++)
            {
                for (int y = 0; y < otherIsland.height; y++)
                {
                    GridCell cell = otherIsland.cellGrid[x, y];
                    if (cell.type != TileType.Empty)
                    {
                        Vector3 cellWorldPos = otherIsland.GridToWorldPosition(new Vector2Int(x, y));
                        cellsToTransfer.Add(new KeyValuePair<Vector3, TileType>(cellWorldPos, cell.type));
                    }
                }
            }
        }

        // Collect blocks
        Block[] blocksToTransfer = otherIsland.GetComponentsInChildren<Block>();
        List<KeyValuePair<Block, Vector3>> blocksWithWorldPos = new List<KeyValuePair<Block, Vector3>>();
        foreach (Block b in blocksToTransfer)
        {
            if (b != null)
            {
                blocksWithWorldPos.Add(new KeyValuePair<Block, Vector3>(b, b.transform.position));
            }
        }

        // Collect non-block visual ground tile transforms
        List<Transform> visualTilesToTransfer = new List<Transform>();
        foreach (Transform child in otherIsland.transform)
        {
            if (child.GetComponent<Block>() == null)
            {
                visualTilesToTransfer.Add(child);
            }
        }

        // Calculate required grid bounds
        Vector2Int minGridPos = new Vector2Int(0, 0);
        Vector2Int maxGridPos = new Vector2Int(width - 1, height - 1);

        foreach (var kvp in cellsToTransfer)
        {
            Vector2Int gridPos = WorldToGridPosition(kvp.Key);
            minGridPos.x = Mathf.Min(minGridPos.x, gridPos.x);
            minGridPos.y = Mathf.Min(minGridPos.y, gridPos.y);
            maxGridPos.x = Mathf.Max(maxGridPos.x, gridPos.x);
            maxGridPos.y = Mathf.Max(maxGridPos.y, gridPos.y);
        }

        if (minGridPos.x < 0 || minGridPos.y < 0)
        {
            ExpandGridIfNeeded(minGridPos);
        }
        if (maxGridPos.x >= width || maxGridPos.y >= height)
        {
            ExpandGridIfNeeded(maxGridPos);
        }

        // Transfer grid cells
        foreach (var kvp in cellsToTransfer)
        {
            Vector2Int targetGridPos = WorldToGridPosition(kvp.Key);
            SetTileTypeAt(targetGridPos, kvp.Value);
        }

        // Reparent visual ground tiles
        foreach (Transform tileTransform in visualTilesToTransfer)
        {
            Vector3 tileWorldPos = tileTransform.position;
            tileTransform.SetParent(this.transform, true);

            Vector2Int tileGridPos = WorldToGridPosition(tileWorldPos);
            tileTransform.localPosition = GridToLocalPosition(tileGridPos, 0.1f);
            tileTransform.localScale = new Vector3(boxScale.x, boxScale.y, 1f);
        }

        // Reparent blocks
        foreach (var kvp in blocksWithWorldPos)
        {
            Block block = kvp.Key;
            Vector3 worldPos = kvp.Value;

            otherIsland.RemoveBlock(block.gridPosition);

            Vector2Int newMainGridPos = WorldToGridPosition(worldPos);

            block.transform.SetParent(this.transform, true);
            block.currentIsland = this;
            block.gridPosition = newMainGridPos;

            SetTileTypeAt(newMainGridPos, TileType.Ground);
            RegisterBlock(block, newMainGridPos);

            block.transform.localPosition = GridToLocalPosition(newMainGridPos, block.transform.localPosition.z);
        }

        Destroy(otherIsland.gameObject);
    }

    public void SetTileTypeAt(Vector2Int gridPos, TileType type)
    {
        if (!IsValidLocalPos(gridPos))
        {
            ExpandGridIfNeeded(gridPos);
        }

        if (gridPos.x >= 0 && gridPos.x < width && gridPos.y >= 0 && gridPos.y < height)
        {
            cellGrid[gridPos.x, gridPos.y].type = type;
        }
    }

    public Vector3 GridToWorldPosition(Vector2Int gridPos, float zOffset = 0f)
    {
        Vector3 localPos = GridToLocalPosition(gridPos, zOffset);
        return transform.TransformPoint(localPos);
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