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

        // 1. Calculate intended target local position
        Vector2Int intendedTargetPos = gridPosition + direction;

        // 2. Pre-Check: Calculate intended target world position BEFORE expanding grid
        Vector3 intendedLocalSpacePos = targetIsland.GridToLocalPosition(intendedTargetPos, 0f);
        Vector3 intendedWorldPos = targetIsland.transform.TransformPoint(intendedLocalSpacePos);

        // 3. Check if the target space overlaps with a scale-mismatched island
        if (IsTargetSpaceBlockedByMismatchedIsland(targetIsland, intendedWorldPos))
        {
            return false; // Abort movement & deployment
        }

        // 4. Check if target cell already has ground tile inside current island
        bool targetHasGround = targetIsland.IsValidLocalPos(intendedTargetPos);

        // 5. Ammo Guard
        if (!targetHasGround && deployAmmo <= 0)
        {
            return false;
        }

        // 6. Expand grid for the deployer's target space if needed
        targetIsland.ExpandGridIfNeeded(intendedTargetPos);

        // 7. Read deployer's current position and target cell AFTER expansion
        Vector2Int startPos = this.gridPosition;
        Vector2Int targetPos = startPos + direction;

        // 8. Block Overlap Check inside current island grid
        if (!CanMoveTo(targetPos))
        {
            return false;
        }

        // 9. Deploy Ground Tile if moving into empty space
        if (!targetHasGround)
        {
            deployAmmo--;
            if (groundBlockPrefab != null)
            {
                targetIsland.DeployGroundTileAt(targetPos, groundBlockPrefab);
            }

            CheckAndBridgeAdjacentIslands(targetIsland, targetPos);
        }

        // 10. READ FINAL POSITION POST-MERGE
        Vector2Int finalStartPos = this.gridPosition;
        Vector2Int finalTargetPos = finalStartPos + direction;

        // 11. Execute Movement
        targetIsland.RemoveBlock(finalStartPos);

        this.gridPosition = finalTargetPos;
        this.currentIsland = targetIsland;
        targetIsland.RegisterBlock(this, finalTargetPos);

        this.MoveToGridPosition(finalTargetPos);

        return true;
    }

    /// <summary>
    /// Checks if target location overlaps an external island that has a scale mismatch.
    /// </summary>
    private bool IsTargetSpaceBlockedByMismatchedIsland(GridIsland currentIsland, Vector3 targetWorldPos)
    {
        Vector2 boxSize = new Vector2(
            0.8f * currentIsland.boxScale.x * currentIsland.transform.lossyScale.x,
            0.8f * currentIsland.boxScale.y * currentIsland.transform.lossyScale.y
        );

        Collider2D[] hits = Physics2D.OverlapBoxAll(targetWorldPos, boxSize, currentIsland.transform.eulerAngles.z);

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

            // If an external island exists at the target space and its scale doesn't match, block movement!
            if (detectedIsland != null && detectedIsland != currentIsland)
            {
                if (!currentIsland.HasSameScale(detectedIsland))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private void CheckAndBridgeAdjacentIslands(GridIsland mainIsland, Vector2Int deployedLocalPos)
    {
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
                if (mainIsland.HasSameScale(detectedIsland))
                {
                    mainIsland.MergeOtherIsland(detectedIsland);
                    break;
                }
            }
        }
    }
}