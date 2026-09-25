using System;
using System.Collections.Generic;
using UnityEngine;

public enum TileType
{
    Empty,          // 0: Void / Out of bounds space (Nothing spawns)
    Ground,         // 1: Walkable empty background tile (Spawns emptyCellPrefab)
    Immovable,      // Fixed block
    MovableStatic,  // Static block
    Dynamic,        // Player block
    Joint,          // Joint block
    GroundDeployer
}

[Serializable]
public struct GridCell
{
    public TileType type;
    public int blockColorID;

    public GridCell(TileType type = TileType.Empty, int blockColorID = 0)
    {
        this.type = type;
        this.blockColorID = blockColorID;
    }

    // Helper property to check if this cell counts as playable grid space
    public bool IsActive => type != TileType.Empty;
}

[Serializable]
public class GridIslandData
{
    public int islandID;
    public Vector2Int originPosition;
    public int width = 3;
    public int height = 3;

    // Single integer scale factor
    public int scaleFactorX = 1;
    public int scaleFactorY = 1;
    public List<GridCell> gridData = new List<GridCell>();

    /// <summary>
    /// Gets the GridCell at local (x, y) coordinates safely.
    /// </summary>
    public GridCell GetCell(int x, int y)
    {
        int index = y * width + x;
        if (index >= 0 && index < gridData.Count)
        {
            return gridData[index];
        }
        return new GridCell { type = TileType.Empty };
    }

    /// <summary>
    /// Overload for Vector2Int coordinates.
    /// </summary>
    public GridCell GetCell(Vector2Int pos)
    {
        return GetCell(pos.x, pos.y);
    }

    public void ValidateGridSize()
    {
        int requiredSize = width * height;
        while (gridData.Count < requiredSize)
        {
            gridData.Add(new GridCell { type = TileType.Empty });
        }
        while (gridData.Count > requiredSize)
        {
            gridData.RemoveAt(gridData.Count - 1);
        }
    }
}

[CreateAssetMenu(fileName = "NewMultiGridLevel", menuName = "Puzzle System/Multi-Grid Level Data")]
public class LevelData : ScriptableObject
{
    [Header("Level Info")]
    public int levelNumber = 1;
    public string levelName = "New Level";
    public int parMoves = 15;

    [Header("Grid Islands")]
    public List<GridIslandData> islands = new List<GridIslandData>();

}