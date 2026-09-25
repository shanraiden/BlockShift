using UnityEngine;

public class GroundDeployerBlock : Block
{
    [Header("Deployer Settings")]
    private int deployAmmo;
    [SerializeField] private GameObject groundBlockPrefab;


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

        // 1. Calculate target intent before grid expansion
        Vector2Int intendedTargetPos = gridPosition + direction;

        // 2. Check if target cell already has ground tile
        bool targetHasGround = targetIsland.IsValidLocalPos(intendedTargetPos);

        // 3. Ammo Guard
        if (!targetHasGround && deployAmmo <= 0)
        {
            return false;
        }

        // 4. Expand grid for the deployer's target space if needed
        targetIsland.ExpandGridIfNeeded(intendedTargetPos);

        // 5. Read deployer's current position and target cell AFTER expansion
        Vector2Int startPos = this.gridPosition;
        Vector2Int targetPos = startPos + direction;

        // 6. Block Overlap Check
        if (!CanMoveTo(targetPos))
        {
            return false;
        }

        // 7. Deploy Ground Tile if moving into empty space
        if (!targetHasGround)
        {
          
            deployAmmo--;
            if (groundBlockPrefab != null)
            {
                targetIsland.DeployGroundTileAt(targetPos, groundBlockPrefab);
            }

            // CRITICAL STEP: Bridge / Merge islands BEFORE moving the deployer.
            // If Island 2 merges, Island 1 may expand again and update 'this.gridPosition'!
            CheckAndBridgeAdjacentIslands(targetIsland, targetPos);
        }

        // 8. READ FINAL POSITION POST-MERGE
        // 'this.gridPosition' was automatically shifted by MergeOtherIsland if expansion occurred.
        // We calculate the final destination relative to where the deployer is RIGHT NOW.
        Vector2Int finalStartPos = this.gridPosition;
        Vector2Int finalTargetPos = finalStartPos + direction;

        // 9. Execute Movement to the true post-merge target coordinate
        targetIsland.RemoveBlock(finalStartPos);

        this.gridPosition = finalTargetPos;
        this.currentIsland = targetIsland;
        targetIsland.RegisterBlock(this, finalTargetPos);

        // Animate to the exact post-merge local coordinate
        this.MoveToGridPosition(finalTargetPos);

        return true;
    }

    private void CheckAndBridgeAdjacentIslands(GridIsland mainIsland, Vector2Int deployedLocalPos)
    {
        // Re-calculate deployed tile's EXACT world position post-expansion
        Vector3 localPos = mainIsland.GridToLocalPosition(deployedLocalPos, 0f);
        Vector3 deployedWorldPos = mainIsland.transform.TransformPoint(localPos);

        Vector2 boxSize = new Vector2(
            1.2f * mainIsland.boxScale.x * mainIsland.transform.lossyScale.x,
            1.2f * mainIsland.boxScale.y * mainIsland.transform.lossyScale.y
        );

        Collider2D[] hits = Physics2D.OverlapBoxAll(deployedWorldPos, boxSize, mainIsland.transform.eulerAngles.z);

        foreach (Collider2D hit in hits)
        {
            GridIsland detectedIsland = hit.GetComponentInParent<GridIsland>();

            if (detectedIsland == null)
            {
                Block hitBlock = hit.GetComponent<Block>();
                if (hitBlock != null)
                {
                    detectedIsland = hitBlock.currentIsland;
                }
            }

            if (detectedIsland != null && detectedIsland != mainIsland)
            {
                mainIsland.MergeOtherIsland(detectedIsland);
                break; // Stop after merging
            }
        }
    }
}