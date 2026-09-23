using System.Collections.Generic;
using System.Linq;
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

[System.Serializable]
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

[System.Serializable]
public class GridIslandData
{
    public int islandID = 1;
    public Vector2Int originPosition; // World offset
    public int width = 3;
    public int height = 3;
    public List<GridCell> gridData;

    public GridCell GetCell(int x, int y)
    {
        int index = y * width + x; // Standard 2D to 1D mapping
        if (index >= 0 && index < gridData.Count())
            return gridData[index];
        return new GridCell { type = TileType.Empty };
    }

    public void ValidateGridSize()
    {
        int requiredSize = width * height;

        if (gridData == null)
        {
            gridData = new List<GridCell>();
        }

        // Resize list if width/height changed
        while (gridData.Count < requiredSize)
        {
            // Default new cells to Ground so they aren't skipped as Void
            gridData.Add(new GridCell { type = TileType.Ground });
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