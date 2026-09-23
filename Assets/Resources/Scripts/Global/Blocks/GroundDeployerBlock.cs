using UnityEngine;

public class GroundDeployerBlock : Block
{
    [Header("Deployer Settings")]
    [SerializeField] private int deployAmmo = 3;
    [SerializeField] private GameObject groundBlockPrefab;

    public int RemainingAmmo => deployAmmo;

    public override bool CanPlayerMoveDirectly() => true;
    public override bool IsAffectedByGravity() => false;

    public void ConfigureDeployer(GameObject groundPrefab, int ammo)
    {
        this.groundBlockPrefab = groundPrefab;
        this.deployAmmo = ammo;
    }

    public bool TryMoveAndDeploy(Vector2Int direction)
    {
        if (currentIsland == null) return false;

        GridIsland targetIsland = currentIsland;

        Vector2Int startPos = gridPosition;
        Vector2Int targetPos = startPos + direction;

        // 1. Check if target cell ALREADY has ground before grid expansion
        bool targetHasGround = targetIsland.IsValidLocalPos(targetPos);

        // 2. AMMO GUARD
        if (!targetHasGround && deployAmmo <= 0)
        {
            return false;
        }

        // 3. EXPAND GRID: Pre-expand if moving into negative bounds or new bounds
        targetIsland.ExpandGridIfNeeded(targetPos);

        // 4. Re-read coordinates after origin adjustment
        Vector2Int actualStartPos = gridPosition;
        Vector2Int actualTargetPos = actualStartPos + direction;

        // 5. BLOCK OVERLAP CHECK: Use base CanMoveTo method!
        if (!CanMoveTo(actualTargetPos))
        {
            Debug.Log($"[Deployer] Cannot move to {actualTargetPos} - space occupied by another block!");
            return false;
        }

        // 6. DEPLOYMENT
        if (!targetHasGround)
        {
            deployAmmo--;
            if (groundBlockPrefab != null)
            {
                targetIsland.DeployGroundTileAt(actualTargetPos, groundBlockPrefab);
            }

            // Scan and merge adjacent islands
            CheckAndBridgeAdjacentIslands(targetIsland, actualTargetPos);
        }

        // 7. EXECUTE MOVEMENT
        targetIsland.RemoveBlock(actualStartPos);

        this.gridPosition = actualTargetPos;
        this.currentIsland = targetIsland;
        targetIsland.RegisterBlock(this, actualTargetPos);

        this.MoveToGridPosition(actualTargetPos);

        return true;
    }

    /// <summary>
    /// Scans 4 orthogonal neighbor cells around newly deployed ground to detect and merge adjacent islands.
    /// </summary>
    /// <summary>
    /// Scans neighbor cells around newly deployed ground to detect and merge adjacent islands.
    /// </summary>
    private void CheckAndBridgeAdjacentIslands(GridIsland mainIsland, Vector2Int deployedLocalPos)
    {
        // Convert the local grid coordinate of the newly placed ground into world position
        Vector3 deployedWorldPos = mainIsland.transform.TransformPoint(new Vector3(deployedLocalPos.x, deployedLocalPos.y, 0));

       // Debug.Log($"[Deployer] Scanning for adjacent islands around world pos: {deployedWorldPos}");

        // 1. Overlap scan around the new ground tile (1.5 unit box covers adjacent cells)
        Collider2D[] hits = Physics2D.OverlapBoxAll(deployedWorldPos, Vector2.one * 1.5f, 0f);

        //Debug.Log($"[Deployer] OverlapBox detected {hits.Length} colliders.");

        foreach (Collider2D hit in hits)
        {
            // 2. Find the GridIsland component by checking the parent hierarchy of the hit object
            GridIsland detectedIsland = hit.GetComponentInParent<GridIsland>();

            // Fallback: check if the object itself is a Block pointing to an island
            if (detectedIsland == null)
            {
                Block hitBlock = hit.GetComponent<Block>();
                if (hitBlock != null)
                {
                    detectedIsland = hitBlock.currentIsland;
                }
            }

            if (detectedIsland != null)
            {
               // Debug.Log($"[Deployer] Found island '{detectedIsland.name}' via collider '{hit.name}'");

                // 3. If it's a DIFFERENT island, merge it into mainIsland!
                if (detectedIsland != mainIsland)
                {
                  //  Debug.Log($"<color=green>[Deployer] BRIDGE SUCCESS! Merging '{detectedIsland.name}' into '{mainIsland.name}'...</color>");

                    mainIsland.MergeOtherIsland(detectedIsland);
                    break; // Stop scanning after initiating merge
                }
            }
            else
            {
                //Debug.Log($"[Deployer] Hit collider '{hit.name}' but it is not attached to any GridIsland.");
            }
        }
    }
}