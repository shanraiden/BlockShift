using System.Collections;
using UnityEngine;

public class LevelLoader : MonoBehaviour
{
    [Header("Level Configuration")]
    [SerializeField] private LevelData currentLevel;


    [Header("Scene References")]
    [SerializeField] private MultiGridManager multiGridManager;
    [SerializeField] private CameraController cameraController;
    [Header("Gravity Manager")]
    [SerializeField] private GravityManager gravityManager;

    [Header("Prefabs")]
    [SerializeField] private GameObject immovablePrefab;
    [SerializeField] private GameObject movableStaticPrefab;
    [SerializeField] private GameObject dynamicPrefab;
    [SerializeField] private GameObject jointPrefab;
    [SerializeField] private GameObject emptyCellPrefab; // Background grid tile prefab
    private void Awake()
    {
        // Automatically fetch MultiGridManager if attached to the same GameObject
        if (multiGridManager == null)
        {
            multiGridManager = GetComponent<MultiGridManager>();
        }
    }

    private IEnumerator Start()
    {
        if (currentLevel != null)
        {
            // 1. Fully load level and instantiate all islands/blocks
            LoadLevel(currentLevel);

            // 2. Wait 1 frame so Awake/Start routines on spawned objects finalize
            yield return null;

            // 3. NOW run gravity on fully registered islands
            if (gravityManager != null && multiGridManager != null)
            {
                yield return StartCoroutine(gravityManager.ApplyGravityRoutine(multiGridManager.GetActiveIslands()));
            }
        }
    }
    public void LoadLevel(LevelData levelToLoad)
    {
        currentLevel = levelToLoad;

        // 1. Clear previous islands from MultiGridManager
        if (multiGridManager != null)
        {
            multiGridManager.ClearIslands();
        }

        // Trigger immediate gravity check for mid-air dynamic blocks on level load
        if (gravityManager != null && multiGridManager != null)
        {
            StartCoroutine(gravityManager.ApplyGravityRoutine(multiGridManager.GetActiveIslands()));
        }
        

        // 2. Loop through each GridIslandData in LevelData
        foreach (var islandData in currentLevel.islands)
        {
            // Create a parent GameObject for this island
            GameObject islandGO = new GameObject($"Island_{islandData.islandID}");

            // Add and initialize the GridIsland component
            GridIsland islandScript = islandGO.AddComponent<GridIsland>();
            islandScript.InitializeIsland(
                islandData.islandID,
                islandData.width,
                islandData.height,
                islandData.originPosition
            );

            // 3. Register the island into MultiGridManager
            if (multiGridManager != null)
            {
                multiGridManager.RegisterIsland(islandScript);
            }

            // 4. Spawn blocks and register them inside this island
            for (int x = 0; x < islandData.width; x++)
            {
                for (int y = 0; y < islandData.height; y++)
                {
                    GridCell cell = islandData.GetCell(x, y);

                    Vector3 worldPos = new Vector3(
                        islandData.originPosition.x + x,
                        islandData.originPosition.y + y,
                        0
                    );

                    // 1. ALWAYS spawn the background tile prefab underneath
                    if (emptyCellPrefab != null)
                    {
                        // Position slightly behind blocks (z = 0.1f) to prevent z-fighting
                        Vector3 bgPos = new Vector3(worldPos.x, worldPos.y, 0.1f);
                        Instantiate(emptyCellPrefab, bgPos, Quaternion.identity, islandGO.transform);
                    }

                    GameObject blockGO = null;

                    // 2. Spawn specific Block on top of the background if not empty
                    switch (cell.type)
                    {
                        case TileType.Immovable:
                            if (immovablePrefab != null)
                                blockGO = Instantiate(immovablePrefab, worldPos, Quaternion.identity, islandGO.transform);
                            break;

                        case TileType.MovableStatic:
                            if (movableStaticPrefab != null)
                                blockGO = Instantiate(movableStaticPrefab, worldPos, Quaternion.identity, islandGO.transform);
                            break;

                        case TileType.Dynamic:
                            if (dynamicPrefab != null)
                                blockGO = Instantiate(dynamicPrefab, worldPos, Quaternion.identity, islandGO.transform);
                            break;

                        case TileType.Joint:
                            if (jointPrefab != null)
                                blockGO = Instantiate(jointPrefab, worldPos, Quaternion.identity, islandGO.transform);
                            break;
                    }

                    // Register Block component if instantiated
                    if (blockGO != null && blockGO.TryGetComponent<Block>(out Block blockScript))
                    {
                        blockScript.gridPosition = new Vector2Int(x, y);
                        islandScript.RegisterBlock(blockScript, blockScript.gridPosition);
                    }
                }
            }
        }

        // 5. Adjust Camera to frame all active islands
        if (cameraController != null)
        {
            //cameraController.AdjustCameraToMultiGrid(currentLevel.islands);
        }
    }

    /// <summary>
    /// Reloads the currently assigned LevelData asset back to its default state.
    /// </summary>
    public void ReloadCurrentLevel()
    {
          if (currentLevel != null)
        {
            StopAllCoroutines();
            StartCoroutine(RestartLevelRoutine());
        }
    }

    private IEnumerator RestartLevelRoutine()
    {
        // 1. Re-instantiate all islands and blocks from the pristine LevelData asset
         LoadLevel(currentLevel);

        // 2. Wait 1 frame so grid arrays and object registrations finalize
        yield return null;

        // 3. Resolve initial gravity for airborne blocks in default layout
        if (gravityManager != null && multiGridManager != null)
        {
            yield return StartCoroutine(gravityManager.ApplyGravityRoutine(multiGridManager.GetActiveIslands()));
        }
    }
}