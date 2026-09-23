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
    [SerializeField] private GameObject emptyCellPrefab;              // Ground tile (Walkable background)
    [SerializeField] private GameObject immovablePrefab;              // IMM
    [SerializeField] private GameObject movableStaticPrefab;          // STA
    [SerializeField] private GameObject dynamicPrefab;                // DYN
    [SerializeField] private GameObject jointPrefab;                  // JNT
    [SerializeField] private GameObject groundDeployerBlockPrefab;     // Deployer Block

    private void Awake()
    {
        if (multiGridManager == null)
            multiGridManager = GetComponent<MultiGridManager>();
    }

    private IEnumerator Start()
    {
        if (currentLevel != null)
        {
            LoadLevel(currentLevel);
            yield return null; // Wait 1 frame for Awake/Start on newly instantiated objects

            if (gravityManager != null && multiGridManager != null)
            {
                yield return StartCoroutine(gravityManager.ApplyGravityRoutine(multiGridManager.GetActiveIslands()));
            }
        }
    }

    public void LoadLevel(LevelData levelToLoad)
    {
        currentLevel = levelToLoad;

        if (multiGridManager != null)
        {
            multiGridManager.ClearIslands();
        }

        foreach (var islandData in currentLevel.islands)
        {
            // 1. Position the Island parent at its origin in world space
            GameObject islandGO = new GameObject($"Island_{islandData.islandID}");
            islandGO.transform.position = new Vector3(islandData.originPosition.x, islandData.originPosition.y, 0f);

            GridIsland islandScript = islandGO.AddComponent<GridIsland>();

            GridCell[,] structGrid = new GridCell[islandData.width, islandData.height];
            for (int x = 0; x < islandData.width; x++)
            {
                for (int y = 0; y < islandData.height; y++)
                {
                    structGrid[x, y] = islandData.GetCell(x, y);
                }
            }

            islandScript.InitializeIsland(
                islandData.islandID,
                islandData.width,
                islandData.height,
                islandData.originPosition,
                structGrid
            );

            if (multiGridManager != null)
            {
                multiGridManager.RegisterIsland(islandScript);
            }

            // 2. Spawn Tiles & Blocks RELATIVE to the Island parent
            for (int x = 0; x < islandData.width; x++)
            {
                for (int y = 0; y < islandData.height; y++)
                {
                    GridCell cell = structGrid[x, y];

                    // ONLY skip if the cell is explicitly set to Void (Empty)
                    if (cell.type == TileType.Empty) continue;

                    // Local position within island container
                    Vector3 localPos = new Vector3(x, y, 0f);

                    // 1. Always spawn background Ground tile for non-empty cells
                    if (emptyCellPrefab != null)
                    {
                        GameObject bgGO = Instantiate(emptyCellPrefab, islandGO.transform);
                        bgGO.transform.localPosition = new Vector3(localPos.x, localPos.y, 0.1f);
                        bgGO.transform.localRotation = Quaternion.identity;
                        bgGO.transform.localScale = Vector3.one;
                    }

                    // 2. Spawn Block if cell type is a Block
                    if (cell.type != TileType.Ground)
                    {
                        GameObject prefabToSpawn = GetPrefabForTileType(cell.type);

                        if (prefabToSpawn != null)
                        {
                            GameObject blockGO = Instantiate(prefabToSpawn, islandGO.transform);
                            blockGO.transform.localPosition = localPos;
                            blockGO.transform.localRotation = Quaternion.identity;
                            blockGO.transform.localScale = Vector3.one;

                            if (blockGO.TryGetComponent<Block>(out Block blockScript))
                            {
                                blockScript.gridPosition = new Vector2Int(x, y);
                                islandScript.RegisterBlock(blockScript, blockScript.gridPosition);

                                // Special Setup: Inject Ground Prefab and initial ammo if this is a GroundDeployerBlock
                                if (blockScript is GroundDeployerBlock deployerBlock)
                                {
                                    int initialAmmo = 3; // Default ammo per deployer
                                    deployerBlock.ConfigureDeployer(emptyCellPrefab, initialAmmo);
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    private GameObject GetPrefabForTileType(TileType type)
    {
        return type switch
        {
            TileType.Immovable => immovablePrefab,
            TileType.MovableStatic => movableStaticPrefab,
            TileType.Dynamic => dynamicPrefab,
            TileType.Joint => jointPrefab,
            TileType.GroundDeployer => groundDeployerBlockPrefab,
            _ => null // Ground or Empty spawns no extra block prefab
        };
    }

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
        LoadLevel(currentLevel);
        yield return null;

        if (gravityManager != null && multiGridManager != null)
        {
            yield return StartCoroutine(gravityManager.ApplyGravityRoutine(multiGridManager.GetActiveIslands()));
        }
    }
}