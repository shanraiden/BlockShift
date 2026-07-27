using UnityEngine;

[ExecuteInEditMode]
public class LevelDataGizmoDrawer : MonoBehaviour
{
    [Header("Target Level Asset")]
    public LevelData levelData;

    [Header("Visual Settings")]
    public bool showGizmos = true;
    public bool showTileLabels = false;

    private void Start()
    {
        showGizmos = false;
    }

    

    private void OnDrawGizmos()
    {
        if (!showGizmos || levelData == null || levelData.islands == null) return;

        foreach (var island in levelData.islands)
        {
            if (island == null) continue;

            // Draw Island Origin Anchor Marker
            Gizmos.color = Color.magenta;
            Vector3 originCenter = new Vector3(island.originPosition.x, island.originPosition.y, 0);
            Gizmos.DrawSphere(originCenter, 0.15f);

            // Draw Island World Boundary Box
            Gizmos.color = Color.cyan;
            Vector3 islandCenter = new Vector3(
                island.originPosition.x + (island.width / 2f) - 0.5f,
                island.originPosition.y + (island.height / 2f) - 0.5f,
                0
            );
            Gizmos.DrawWireCube(islandCenter, new Vector3(island.width, island.height, 0.1f));

            // Draw Individual Grid Cells
            for (int x = 0; x < island.width; x++)
            {
                for (int y = 0; y < island.height; y++)
                {
                    GridCell cell = island.GetCell(x, y);
                    Vector3 cellPos = new Vector3(
                        island.originPosition.x + x,
                        island.originPosition.y + y,
                        0
                    );

                    // Cell Outline
                    Gizmos.color = new Color(1f, 1f, 1f, 0.15f);
                    Gizmos.DrawWireCube(cellPos, new Vector3(0.95f, 0.95f, 0.1f));

                    // Fill cell based on Block Subclass / Tile Type
                    if (cell.type != TileType.Empty)
                    {
                        Gizmos.color = GetTileGizmoColor(cell.type);
                        Gizmos.DrawCube(cellPos, new Vector3(0.85f, 0.85f, 0.1f));
                    }
                }
            }
        }
    }

    private Color GetTileGizmoColor(TileType type)
    {
        return type switch
        {
            TileType.Immovable => new Color(0.3f, 0.3f, 0.3f, 0.8f),    // Dark Gray
            TileType.MovableStatic => new Color(0.2f, 0.7f, 1f, 0.8f), // Soft Cyan
            TileType.Dynamic => new Color(1f, 0.6f, 0f, 0.8f),         // Orange
            TileType.Joint => new Color(0.8f, 0.2f, 0.8f, 0.8f),       // Magenta
            TileType.GoalTile => new Color(0.2f, 0.9f, 0.2f, 0.8f),     // Green
            TileType.HazardTile => new Color(0.9f, 0.2f, 0.2f, 0.8f),   // Red
            _ => Color.white
        };
    }
}