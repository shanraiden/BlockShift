#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Enemy), true)]
[CanEditMultipleObjects]
public class EnemyEditor : Editor
{
    private const string EnemySpriteFolder = "Assets/Resources/Sprites/Enemies";

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(10);
        EditorGUILayout.BeginVertical(GUI.skin.box);
        EditorGUILayout.LabelField("Enemy GameObject Setup & Scene Builder", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Build child visuals/anchors or spawn ready-to-use Enemy GameObjects directly in the scene.", EditorStyles.miniLabel);
        EditorGUILayout.Space(4);

        if (GUILayout.Button("Setup / Rebuild Child GameObjects & Sprites", GUILayout.Height(26)))
        {
            EnsureAllEnemySpriteAssetsExist();
            foreach (Object t in targets)
            {
                Enemy enemy = t as Enemy;
                if (enemy == null) continue;

                Undo.RegisterFullObjectHierarchyUndo(enemy.gameObject, "Setup Enemy Child GameObjects");
                enemy.SetupDefaultEnemyObjects();
                AssignPersistentSpriteIfNeeded(enemy);
                EditorUtility.SetDirty(enemy);
            }
        }

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Spawn Enemy GameObjects in Scene", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("+ Circle Enemy", GUILayout.Height(28)))
        {
            CreateCircleEnemyInScene();
        }
        if (GUILayout.Button("+ Square Enemy", GUILayout.Height(28)))
        {
            CreateSquareEnemyInScene();
        }
        if (GUILayout.Button("+ Triangle Enemy", GUILayout.Height(28)))
        {
            CreateTriangleEnemyInScene();
        }
        EditorGUILayout.EndHorizontal();

        if (GUILayout.Button("+ Create All 3 Enemy GameObjects (Circle, Square, Triangle)", GUILayout.Height(28)))
        {
            CreateAllThreeEnemiesInScene();
        }

        EditorGUILayout.EndVertical();
    }

    [MenuItem("GameObject/2D Object/BlockShift Enemies/Circle Enemy (Move)", false, 10)]
    private static void MenuCreateCircleEnemy(MenuCommand menuCommand)
    {
        GameObject obj = CreateCircleEnemyInScene();
        GameObjectUtility.SetParentAndAlign(obj, menuCommand.context as GameObject);
    }

    [MenuItem("GameObject/2D Object/BlockShift Enemies/Square Enemy (Block)", false, 11)]
    private static void MenuCreateSquareEnemy(MenuCommand menuCommand)
    {
        GameObject obj = CreateSquareEnemyInScene();
        GameObjectUtility.SetParentAndAlign(obj, menuCommand.context as GameObject);
    }

    [MenuItem("GameObject/2D Object/BlockShift Enemies/Triangle Enemy (Projectile)", false, 12)]
    private static void MenuCreateTriangleEnemy(MenuCommand menuCommand)
    {
        GameObject obj = CreateTriangleEnemyInScene();
        GameObjectUtility.SetParentAndAlign(obj, menuCommand.context as GameObject);
    }

    [MenuItem("GameObject/2D Object/BlockShift Enemies/Create All 3 Enemies", false, 13)]
    private static void MenuCreateAllEnemies(MenuCommand menuCommand)
    {
        CreateAllThreeEnemiesInScene();
    }

    public static GameObject CreateCircleEnemyInScene(Vector3? customPos = null)
    {
        EnsureAllEnemySpriteAssetsExist();
        Vector3 spawnPos = customPos ?? GetSceneSpawnPosition(new Vector3(-2.5f, 0f, 0f));

        GameObject root = new GameObject("Enemy_Circle");
        Undo.RegisterCreatedObjectUndo(root, "Create Circle Enemy");
        root.transform.position = spawnPos;

        Rigidbody2D rb = root.AddComponent<Rigidbody2D>();
        rb.freezeRotation = true;

        CircleCollider2D col = root.AddComponent<CircleCollider2D>();
        col.radius = 0.45f;

        CircleEnemy enemy = root.AddComponent<CircleEnemy>();
        enemy.SetupDefaultEnemyObjects();
        AssignPersistentSpriteIfNeeded(enemy);

        Selection.activeGameObject = root;
        return root;
    }

    public static GameObject CreateSquareEnemyInScene(Vector3? customPos = null)
    {
        EnsureAllEnemySpriteAssetsExist();
        Vector3 spawnPos = customPos ?? GetSceneSpawnPosition(Vector3.zero);

        GameObject root = new GameObject("Enemy_Square");
        Undo.RegisterCreatedObjectUndo(root, "Create Square Enemy");
        root.transform.position = spawnPos;

        Rigidbody2D rb = root.AddComponent<Rigidbody2D>();
        rb.freezeRotation = true;
        rb.mass = 100f;

        BoxCollider2D col = root.AddComponent<BoxCollider2D>();
        col.size = new Vector2(0.95f, 0.95f);

        SquareEnemy enemy = root.AddComponent<SquareEnemy>();
        enemy.SetupDefaultEnemyObjects();
        AssignPersistentSpriteIfNeeded(enemy);

        Selection.activeGameObject = root;
        return root;
    }

    public static GameObject CreateTriangleEnemyInScene(Vector3? customPos = null)
    {
        EnsureAllEnemySpriteAssetsExist();
        Vector3 spawnPos = customPos ?? GetSceneSpawnPosition(new Vector3(2.5f, 0f, 0f));

        GameObject root = new GameObject("Enemy_Triangle");
        Undo.RegisterCreatedObjectUndo(root, "Create Triangle Enemy");
        root.transform.position = spawnPos;

        Rigidbody2D rb = root.AddComponent<Rigidbody2D>();
        rb.freezeRotation = true;

        PolygonCollider2D poly = root.AddComponent<PolygonCollider2D>();
        poly.SetPath(0, new[]
        {
            new Vector2(0f, 0.48f),
            new Vector2(-0.46f, -0.45f),
            new Vector2(0.46f, -0.45f)
        });

        TriangleEnemy enemy = root.AddComponent<TriangleEnemy>();
        enemy.SetupDefaultEnemyObjects();
        AssignPersistentSpriteIfNeeded(enemy);

        Selection.activeGameObject = root;
        return root;
    }

    public static void CreateAllThreeEnemiesInScene()
    {
        Vector3 center = GetSceneSpawnPosition(Vector3.zero);
        GameObject circle = CreateCircleEnemyInScene(center + new Vector3(-2.5f, 0f, 0f));
        GameObject square = CreateSquareEnemyInScene(center);
        GameObject triangle = CreateTriangleEnemyInScene(center + new Vector3(2.5f, 0f, 0f));
        Selection.objects = new Object[] { circle, square, triangle };
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

    private static void AssignPersistentSpriteIfNeeded(Enemy enemy)
    {
        if (enemy == null) return;

        if (enemy is CircleEnemy)
        {
            SpriteRenderer sr = enemy.GetComponentInChildren<SpriteRenderer>(true);
            if (sr != null)
                sr.sprite = LoadOrCreatePersistentSprite(Enemy.EnemyShapeKind.Circle);
        }
        else if (enemy is SquareEnemy)
        {
            SpriteRenderer sr = enemy.GetComponent<SpriteRenderer>();
            if (sr != null)
                sr.sprite = LoadOrCreatePersistentSprite(Enemy.EnemyShapeKind.Square);

            Transform shieldTf = enemy.transform.Find("ShieldVisual");
            if (shieldTf != null && shieldTf.TryGetComponent(out SpriteRenderer shieldSr))
                shieldSr.sprite = LoadOrCreatePersistentSprite(Enemy.EnemyShapeKind.ShieldBar);
        }
        else if (enemy is TriangleEnemy)
        {
            SpriteRenderer sr = enemy.GetComponent<SpriteRenderer>();
            if (sr != null)
                sr.sprite = LoadOrCreatePersistentSprite(Enemy.EnemyShapeKind.Triangle);
        }
    }

    private static void EnsureAllEnemySpriteAssetsExist()
    {
        LoadOrCreatePersistentSprite(Enemy.EnemyShapeKind.Circle);
        LoadOrCreatePersistentSprite(Enemy.EnemyShapeKind.Square);
        LoadOrCreatePersistentSprite(Enemy.EnemyShapeKind.Triangle);
        LoadOrCreatePersistentSprite(Enemy.EnemyShapeKind.ShieldBar);
    }

    private static Sprite LoadOrCreatePersistentSprite(Enemy.EnemyShapeKind kind)
    {
        string assetPath = $"{EnemySpriteFolder}/{kind}.png";
        Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        if (existing != null)
            return existing;

        if (!AssetDatabase.IsValidFolder("Assets/Resources/Sprites"))
        {
            AssetDatabase.CreateFolder("Assets/Resources", "Sprites");
        }
        if (!AssetDatabase.IsValidFolder(EnemySpriteFolder))
        {
            AssetDatabase.CreateFolder("Assets/Resources/Sprites", "Enemies");
        }

        Texture2D tex = Enemy.BuildShapeTexture(kind, 64);
        byte[] pngData = tex.EncodeToPNG();
        Object.DestroyImmediate(tex);

        File.WriteAllBytes(assetPath, pngData);
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = 64f;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
    }
}
#endif

