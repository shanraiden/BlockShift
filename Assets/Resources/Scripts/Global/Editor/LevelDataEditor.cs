#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(LevelData))]
public class LevelDataEditor : Editor
{
    private TileType selectedPaintTool = TileType.MovableStatic;

    public override void OnInspectorGUI()
    {
        LevelData level = (LevelData)target;

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("Level Configuration", EditorStyles.boldLabel);
        level.levelNumber = EditorGUILayout.IntField("Level Number", level.levelNumber);
        level.levelName = EditorGUILayout.TextField("Level Name", level.levelName);
        level.parMoves = EditorGUILayout.IntField("Par Moves", level.parMoves);

        EditorGUILayout.Space(10);

        // --- PAINT PALETTE SELECTOR ---
        EditorGUILayout.BeginVertical(GUI.skin.box);
        EditorGUILayout.LabelField("Block Paint Tool", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Select a block type below, then click any grid button to paint:", EditorStyles.miniLabel);

        EditorGUILayout.BeginHorizontal();
        DrawPaletteButton("Empty", TileType.Empty, Color.gray);
        DrawPaletteButton("Immovable", TileType.Immovable, new Color(0.4f, 0.4f, 0.4f));
        DrawPaletteButton("Static", TileType.MovableStatic, new Color(0.3f, 0.7f, 1f));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        DrawPaletteButton("Dynamic", TileType.Dynamic, new Color(1f, 0.6f, 0f));
        DrawPaletteButton("Joint", TileType.Joint, new Color(0.8f, 0.3f, 0.8f));
        DrawPaletteButton("Goal", TileType.GoalTile, new Color(0.3f, 0.9f, 0.3f));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(15);
        EditorGUILayout.LabelField("Islands Management", EditorStyles.boldLabel);

        // Add Island Button
        if (GUILayout.Button("+ Add New Island", GUILayout.Height(30)))
        {
            Undo.RecordObject(level, "Add Island");
            int newID = level.islands.Count + 1;
            level.islands.Add(new GridIslandData
            {
                islandID = newID,
                width = 3,
                height = 3,
                originPosition = new Vector2Int((newID - 1) * 5, 0)
            });
            EditorUtility.SetDirty(level);
        }

        EditorGUILayout.Space(10);

        // Render Islands
        for (int i = 0; i < level.islands.Count; i++)
        {
            GridIslandData island = level.islands[i];

            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Island #{island.islandID}", EditorStyles.boldLabel);

            GUI.backgroundColor = new Color(1f, 0.3f, 0.3f);
            if (GUILayout.Button("Delete Island", GUILayout.Width(100)))
            {
                Undo.RecordObject(level, "Delete Island");
                level.islands.RemoveAt(i);
                EditorUtility.SetDirty(level);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                break;
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            island.islandID = EditorGUILayout.IntField("Island ID", island.islandID);
            island.originPosition = EditorGUILayout.Vector2IntField("Origin (World Pos)", island.originPosition);
            island.width = Mathf.Max(1, EditorGUILayout.IntField("Width", island.width));
            island.height = Mathf.Max(1, EditorGUILayout.IntField("Height", island.height));

            island.ValidateGridSize();

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Grid View (Top-Down)", EditorStyles.miniBoldLabel);

            // Draw Interactive Grid Buttons
            for (int y = island.height - 1; y >= 0; y--)
            {
                EditorGUILayout.BeginHorizontal();
                for (int x = 0; x < island.width; x++)
                {
                    int index = y * island.width + x;
                    GridCell cell = island.gridData[index];

                    Color originalBg = GUI.backgroundColor;
                    GUI.backgroundColor = GetTileColor(cell.type);

                    string buttonText = cell.type == TileType.Empty ? "•" : cell.type.ToString().Substring(0, 3);

                    if (GUILayout.Button(buttonText, GUILayout.Width(50), GUILayout.Height(35)))
                    {
                        Undo.RecordObject(level, "Paint Tile");

                        // Right-click or shift-click resets to empty; left-click paints selected tool
                        if (Event.current.button == 1)
                        {
                            cell.type = TileType.Empty;
                        }
                        else
                        {
                            cell.type = selectedPaintTool;
                        }

                        island.gridData[index] = cell;
                        EditorUtility.SetDirty(level);
                        SceneView.RepaintAll(); // Refresh Scene View Gizmos live
                    }

                    GUI.backgroundColor = originalBg;
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(10);
        }

        if (GUI.changed)
        {
            EditorUtility.SetDirty(level);
            SceneView.RepaintAll();
        }
    }

    private void DrawPaletteButton(string label, TileType type, Color color)
    {
        Color orig = GUI.backgroundColor;

        // Highlight active painting tool
        if (selectedPaintTool == type)
        {
            GUI.backgroundColor = color * 1.3f;
        }
        else
        {
            GUI.backgroundColor = color * 0.7f;
        }

        if (GUILayout.Button(label, GUILayout.Height(25)))
        {
            selectedPaintTool = type;
        }

        GUI.backgroundColor = orig;
    }

    private Color GetTileColor(TileType type)
    {
        return type switch
        {
            TileType.Empty => new Color(0.3f, 0.3f, 0.3f),
            TileType.Immovable => new Color(0.5f, 0.5f, 0.5f),
            TileType.MovableStatic => new Color(0.3f, 0.7f, 1f),
            TileType.Dynamic => new Color(1f, 0.6f, 0f),
            TileType.Joint => new Color(0.8f, 0.3f, 0.8f),
            TileType.GoalTile => new Color(0.3f, 0.9f, 0.3f),
            TileType.HazardTile => new Color(0.9f, 0.3f, 0.3f),
            _ => Color.white
        };
    }
}
#endif