#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ShadowSpikes2D))]
[CanEditMultipleObjects]
public class ShadowSpikes2DEditor : Editor
{
    private const string MechanicsSpriteFolder = "Assets/Resources/Sprites/Mechanics";
    private const string SpikeSpritePath = "Assets/Resources/Sprites/Mechanics/Spike.png";

    public override void OnInspectorGUI()
    {
        ShadowSpikes2D spikes = (ShadowSpikes2D)target;

        // Live State Indicator
        EditorGUILayout.BeginVertical(GUI.skin.box);
        EditorGUILayout.LabelField("Shadow Spikes Status", EditorStyles.boldLabel);
        string statusText = spikes.CurrentState switch
        {
            ShadowSpikes2D.SpikeState.FrozenByWhiteShadow => "Frozen by WhiteSolidIce Shadow (Safe Walkable Platform)",
            ShadowSpikes2D.SpikeState.PhasedByBlackShadow => "Phased / Dissolved by BlackEternal Shadow (Harmless)",
            ShadowSpikes2D.SpikeState.WeightedByPickupObject => "Weighed Down by PickUp Object (Suppressed)",
            ShadowSpikes2D.SpikeState.RetractedIdle => "Retracted (Idle Cycle)",
            _ => "Active & Lethal"
        };
        EditorGUILayout.HelpBox(statusText, spikes.IsLethal ? MessageType.Warning : MessageType.Info);
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(4);
        DrawDefaultInspector();

        EditorGUILayout.Space(10);
        EditorGUILayout.BeginVertical(GUI.skin.box);
        EditorGUILayout.LabelField("Shadow Spikes Builder & Scene Tools", EditorStyles.boldLabel);

        if (GUILayout.Button("Rebuild Spike Visuals & Auto-Fit Collider", GUILayout.Height(26)))
        {
            EnsurePersistentSpikeSpriteAsset();
            foreach (Object obj in targets)
            {
                ShadowSpikes2D s = obj as ShadowSpikes2D;
                if (s == null) continue;

                Undo.RegisterFullObjectHierarchyUndo(s.gameObject, "Rebuild Shadow Spikes");
                s.SetupSpikeGameObjects();
                AssignPersistentSpriteToChildren(s);
                EditorUtility.SetDirty(s);
            }
        }

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Spawn Shadow Spikes in Scene", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("+ Single Spike", GUILayout.Height(26)))
        {
            CreateShadowSpikesInScene(1);
        }
        if (GUILayout.Button("+ 3-Spike Strip", GUILayout.Height(26)))
        {
            CreateShadowSpikesInScene(3);
        }
        if (GUILayout.Button("+ 5-Spike Strip", GUILayout.Height(26)))
        {
            CreateShadowSpikesInScene(5);
        }
        EditorGUILayout.EndHorizontal();

        if (GUILayout.Button("+ Spawn PickUp Suppressor Block (Test Counterplay)", GUILayout.Height(24)))
        {
            CreateTestPickupBlockInScene();
        }

        EditorGUILayout.EndVertical();
    }

    [MenuItem("GameObject/2D Object/BlockShift Mechanics/Shadow Spikes (Single)", false, 20)]
    private static void MenuCreateSingleSpike(MenuCommand cmd)
    {
        GameObject obj = CreateShadowSpikesInScene(1);
        GameObjectUtility.SetParentAndAlign(obj, cmd.context as GameObject);
    }

    [MenuItem("GameObject/2D Object/BlockShift Mechanics/Shadow Spikes Strip (3 Spikes)", false, 21)]
    private static void MenuCreateThreeSpikes(MenuCommand cmd)
    {
        GameObject obj = CreateShadowSpikesInScene(3);
        GameObjectUtility.SetParentAndAlign(obj, cmd.context as GameObject);
    }

    [MenuItem("GameObject/2D Object/BlockShift Mechanics/Shadow Spikes Strip (5 Spikes)", false, 22)]
    private static void MenuCreateFiveSpikes(MenuCommand cmd)
    {
        GameObject obj = CreateShadowSpikesInScene(5);
        GameObjectUtility.SetParentAndAlign(obj, cmd.context as GameObject);
    }

    [MenuItem("GameObject/2D Object/BlockShift Mechanics/PickUp Suppressor Block", false, 23)]
    private static void MenuCreatePickupBlock(MenuCommand cmd)
    {
        GameObject obj = CreateTestPickupBlockInScene();
        GameObjectUtility.SetParentAndAlign(obj, cmd.context as GameObject);
    }

    public static GameObject CreateShadowSpikesInScene(int count)
    {
        EnsurePersistentSpikeSpriteAsset();
        Vector3 pos = GetSceneSpawnPosition(Vector3.zero);

        GameObject root = new GameObject(count == 1 ? "ShadowSpikes" : $"ShadowSpikes_x{count}");
        Undo.RegisterCreatedObjectUndo(root, "Create Shadow Spikes");
        root.transform.position = pos;

        BoxCollider2D col = root.AddComponent<BoxCollider2D>();
        col.isTrigger = true;

        ShadowSpikes2D spikes = root.AddComponent<ShadowSpikes2D>();
        SerializedObject so = new SerializedObject(spikes);
        SerializedProperty countProp = so.FindProperty("spikeCount");
        if (countProp != null)
        {
            countProp.intValue = Mathf.Clamp(count, 1, 20);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        spikes.SetupSpikeGameObjects();
        AssignPersistentSpriteToChildren(spikes);

        Selection.activeGameObject = root;
        return root;
    }

    public static GameObject CreateTestPickupBlockInScene()
    {
        Vector3 pos = GetSceneSpawnPosition(new Vector3(0f, 1.5f, 0f));

        GameObject block = new GameObject("PickUp_SuppressorBlock");
        Undo.RegisterCreatedObjectUndo(block, "Create PickUp Suppressor Block");
        block.transform.position = pos;

        Rigidbody2D rb = block.AddComponent<Rigidbody2D>();
        rb.mass = 2f;

        BoxCollider2D col = block.AddComponent<BoxCollider2D>();
        col.size = new Vector2(0.9f, 0.9f);

        SpriteRenderer sr = block.AddComponent<SpriteRenderer>();
        sr.sprite = EnsurePersistentSquareSpriteAsset();
        sr.color = new Color(0.95f, 0.75f, 0.25f, 1f);

        Selection.activeGameObject = block;
        return block;
    }

    private static Sprite EnsurePersistentSquareSpriteAsset()
    {
        const string squarePath = "Assets/Resources/Sprites/Mechanics/PickupBlock.png";
        Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(squarePath);
        if (existing != null) return existing;

        EnsureFolderExists();
        Texture2D tex = Enemy.BuildShapeTexture(Enemy.EnemyShapeKind.Square, 64);
        byte[] pngData = tex.EncodeToPNG();
        Object.DestroyImmediate(tex);

        File.WriteAllBytes(squarePath, pngData);
        AssetDatabase.ImportAsset(squarePath, ImportAssetOptions.ForceUpdate);

        TextureImporter importer = AssetImporter.GetAtPath(squarePath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = 64f;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(squarePath);
    }

    private static void EnsureFolderExists()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources/Sprites"))
        {
            AssetDatabase.CreateFolder("Assets/Resources", "Sprites");
        }
        if (!AssetDatabase.IsValidFolder(MechanicsSpriteFolder))
        {
            AssetDatabase.CreateFolder("Assets/Resources/Sprites", "Mechanics");
        }
    }

    private static Vector3 GetSceneSpawnPosition(Vector3 offset)
    {
        if (SceneView.lastActiveSceneView != null)
        {
            Vector3 pivot = SceneView.lastActiveSceneView.pivot;
            return new Vector3(pivot.x, pivot.y, 0f) + offset;
        }
        return offset;
    }

    private static void AssignPersistentSpriteToChildren(ShadowSpikes2D spikes)
    {
        if (spikes == null) return;

        Sprite persistentSprite = EnsurePersistentSpikeSpriteAsset();
        if (persistentSprite == null) return;

        SpriteRenderer[] renderers = spikes.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                renderers[i].sprite = persistentSprite;
            }
        }
    }

    private static Sprite EnsurePersistentSpikeSpriteAsset()
    {
        Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(SpikeSpritePath);
        if (existing != null)
            return existing;

        EnsureFolderExists();

        Texture2D tex = ShadowSpikes2D.BuildSpikeTexture(64);
        byte[] pngData = tex.EncodeToPNG();
        Object.DestroyImmediate(tex);

        File.WriteAllBytes(SpikeSpritePath, pngData);
        AssetDatabase.ImportAsset(SpikeSpritePath, ImportAssetOptions.ForceUpdate);

        TextureImporter importer = AssetImporter.GetAtPath(SpikeSpritePath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = 64f;
            importer.spritePivot = new Vector2(0.5f, 0.25f);
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(SpikeSpritePath);
    }
}
#endif
