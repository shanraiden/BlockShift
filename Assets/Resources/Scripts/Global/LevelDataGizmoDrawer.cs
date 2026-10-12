using UnityEngine;

[ExecuteInEditMode]
public class LevelDataGizmoDrawer : MonoBehaviour
{
    [Header("Target Level Asset")]
    public LevelData levelData;

    [Header("Visual Settings")]
    public bool showGizmos = true;
    public bool showTileLabels = false;

    private void OnDrawGizmos()
    {
        if (!showGizmos || levelData == null || levelData.islands == null) return;

        foreach (var island in levelData.islands)
        {
            if (island == null) continue;

            int scaleX = Mathf.Max(1, island.scaleFactorX);
            int scaleY = Mathf.Max(1, island.scaleFactorY);

            // Draw Island Origin Anchor Marker
            Gizmos.color = Color.magenta;
            Vector3 originCenter = new Vector3(island.originPosition.x, island.originPosition.y, 0);
            Gizmos.DrawSphere(originCenter, 0.15f * Mathf.Min(scaleX, scaleY));

            // Draw Island World Boundary Box
            Gizmos.color = Color.cyan;
            float totalWidth = island.width * scaleX;
            float totalHeight = island.height * scaleY;

            Vector3 islandCenter = new Vector3(
                island.originPosition.x + (totalWidth / 2f) - (scaleX / 2f),
                island.originPosition.y + (totalHeight / 2f) - (scaleY / 2f),
                0
            );
            Gizmos.DrawWireCube(islandCenter, new Vector3(totalWidth, totalHeight, 0.1f));

            // Draw Individual Grid Cells
            for (int x = 0; x < island.width; x++)
            {
                for (int y = 0; y < island.height; y++)
                {
                    GridCell cell = island.GetCell(x, y);

                    // Fixed missing multiplication operators (*)
                    Vector3 cellPos = new Vector3(
                        island.originPosition.x + (x * scaleX),
                        island.originPosition.y + (y * scaleY),
                        0
                    );

                    if (cell.type == TileType.MovableStaticTriR || cell.type == TileType.MovableStaticTriL)
                    {
                        // --- DRAW TRIANGLE FOR MovableStaticTri ---

                        // Define triangle vertices (pointing up inside the cell bounds)

                        Vector3 top = cellPos + new Vector3((cell.type == TileType.MovableStaticTriR)? 1f : -1f, 0.475f * scaleY, 0f);
                        Vector3 bottomLeft = cellPos + new Vector3(-0.475f * scaleX, -0.475f * scaleY, 0f);
                        Vector3 bottomRight = cellPos + new Vector3(0.475f * scaleX, -0.475f * scaleY, 0f);

                        // Triangle Outline
                        Gizmos.color = new Color(1f, 1f, 1f, 0.25f);
                        Gizmos.DrawLine(top, bottomLeft);
                        Gizmos.DrawLine(bottomLeft, bottomRight);
                        Gizmos.DrawLine(bottomRight, top);

                        
                    }
                    else
                    {
                        // --- STANDARD BOX DRAWING FOR OTHER TILES ---

                        // Cell Outline
                        Gizmos.color = new Color(1f, 1f, 1f, 0.15f);
                        Gizmos.DrawWireCube(cellPos, new Vector3(0.95f * scaleX, 0.95f * scaleY, 0.1f));

                        // Fill cell based on Block Subclass / Tile Type
                        if (cell.type != TileType.Empty)
                        {
                            Gizmos.color = GetTileGizmoColor(cell.type);
                            Gizmos.DrawCube(cellPos, new Vector3(0.85f * scaleX, 0.85f * scaleY, 0.1f));
                        }
                    }
                }
            }

        }
    }

    private Color GetTileGizmoColor(TileType type)
    {
        return type switch
        {
            TileType.Ground => new Color(0.6f, 0.6f, 0.6f, 0.8f),
            TileType.Immovable => new Color(0.3f, 0.3f, 0.3f, 0.8f),
            TileType.MovableStatic => new Color(0.2f, 0.7f, 1f, 0.8f),
            TileType.Dynamic => new Color(1f, 0.6f, 0f, 0.8f),
            TileType.Joint => new Color(0.8f, 0.2f, 0.8f, 0.8f),
            TileType.GroundDeployer => new Color(0.4f, 0.7f, 0.1f, 0.8f),
            TileType.Link => new Color(1f, 0.5f, 0.2f, 0.8f),
            TileType.MovableStaticTriR =>    new Color(1f,0.2f,0.3f),
            TileType.MovableStaticTriL => new Color(1f, 0.2f, 0.3f),
            _ => Color.white
        };
    }
}