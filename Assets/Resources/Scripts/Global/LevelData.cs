using System.Collections.Generic;
using UnityEngine;

public enum TileType
{
    Empty,              // No block present
    Immovable,          // ImmovableBlock (Fixed Anchor)
    MovableStatic,      // MovableStaticBlock (Floats, no gravity)
    Dynamic,            // DynamicBlock (Falls with gravity)
    Joint,              // JointBlock (Links with ImmovableBlocks)
    GoalTile,           // Win condition tile
    HazardTile          // Obstacle/Hazard
}

[System.Serializable]
public struct GridCell
{
    public TileType type;
    public int blockColorID;
}

[System.Serializable]
public class GridIslandData
{
    public int islandID = 1;
    public Vector2Int originPosition; // World offset
    public int width = 3;
    public int height = 3;
    public GridCell[] gridData;

    public GridCell GetCell(int x, int y)
    {
        int index = y * width + x;
        if (gridData != null && index >= 0 && index < gridData.Length)
        {
            return gridData[index];
        }
        return new GridCell { type = TileType.Empty };
    }

    public void ValidateGridSize()
    {
        int targetSize = width * height;
        if (gridData == null || gridData.Length != targetSize)
        {
            System.Array.Resize(ref gridData, targetSize);
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