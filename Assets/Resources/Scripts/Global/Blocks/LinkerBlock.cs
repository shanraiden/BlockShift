using System.Collections.Generic;
using UnityEngine;

public class LinkerBlock : Block
{

    [Header("Lock State Settings")]
    [SerializeField] private new Color defaultColor = Color.white;
    [SerializeField] private Color lockedColor = new Color(0.2f, 0.8f, 1f); // Highlight color when linked & locked
    [SerializeField] private SpriteRenderer blockSpriteRenderer;

    private bool isLocked = false;
    private GridIsland linkedTargetIsland = null;

    public bool IsLocked => isLocked;
    public GridIsland LinkedTargetIsland => linkedTargetIsland;

    public override bool CanPlayerMoveDirectly() => !isLocked; // Prevents movement while locked
    public override bool IsAffectedByGravity() => false;

    private new void Awake()
    {
        if (blockSpriteRenderer == null)
        {
            blockSpriteRenderer = GetComponent<SpriteRenderer>();
        }
        UpdateVisuals();
    }

    /// <summary>
    /// Locks the LinkerBlock, updates visuals, and registers the active link.
    /// </summary>
    public void LockLink(GridIsland targetIsland)
    {
        isLocked = true;
        linkedTargetIsland = targetIsland;
        currentIsland.isLinked = true;
        targetIsland.isLinked = true;
        UpdateVisuals();
        Debug.Log($"[LinkerBlock] Locked link with Island ID: {targetIsland.islandID}");
        currentIsland.highLightThisGrid(lockedColor);
        linkedTargetIsland.highLightThisGrid(lockedColor);
        GetComponent<SpriteRenderer>().color = Color.yellow;
    }

    /// <summary>
    /// Unlocks the LinkerBlock and clears the bridge link.
    /// </summary>
    public void UnlockLink()
    {
        if (currentIsland != null && linkedTargetIsland != null)
        {
            // Unlink islands in your island manager/grid tracking
            currentIsland.UnlinkIsland(linkedTargetIsland);
        }
        currentIsland.isLinked = false;
        linkedTargetIsland.isLinked = false;
        linkedTargetIsland.highLightThisGrid(defaultColor);
        currentIsland.highLightThisGrid(defaultColor);
        isLocked = false;
        linkedTargetIsland = null;
        UpdateVisuals();
        Debug.Log("[LinkerBlock] Link unlocked by player tap/click.");
        GetComponent<SpriteRenderer>().color = Color.white;


    }

    private void UpdateVisuals()
    {
        if (blockSpriteRenderer != null)
        {
            blockSpriteRenderer.color = isLocked ? lockedColor : defaultColor;
        }
    }

    /// <summary>
    /// Scans strictly in the specified move direction for adjacent scale-mismatched islands.
    /// </summary>
    /// <summary>
    /// Detects adjacent scale-mismatched islands strictly in the direction of movement using direct line/point checks.
    /// </summary>
    public List<GridIsland> DetectNearbyIslands(Vector2Int? moveDirection = null)
    {
        List<GridIsland> mismatchedIslands = new List<GridIsland>();

        if (currentIsland == null || !moveDirection.HasValue || moveDirection.Value == Vector2Int.zero)
        {
            return mismatchedIslands;
        }

        Physics2D.SyncTransforms();

        Vector2Int direction = moveDirection.Value;
        Vector2Int targetGridPos = gridPosition + direction;

        // 1. Calculate source and target world points along the move direction
        Vector3 startWorldPos = currentIsland.GridToWorldPosition(gridPosition);
        Vector3 targetWorldPos = currentIsland.GridToWorldPosition(targetGridPos);

        // Visual debugging in Scene view
        Debug.DrawLine(startWorldPos, targetWorldPos, Color.red, 1f);

        // 2. Raycast directly along the directional line toward the adjacent grid space
        Vector3 rayDir = (targetWorldPos - startWorldPos).normalized;
        float rayDistance = Vector3.Distance(startWorldPos, targetWorldPos);

        RaycastHit2D[] hits = Physics2D.RaycastAll(startWorldPos, rayDir, rayDistance);

        foreach (RaycastHit2D hit in hits)
        {
            if (hit.collider == null) continue;

            GridIsland detectedIsland = hit.collider.GetComponentInParent<GridIsland>();

            if (detectedIsland == null)
            {
                Block hitBlock = hit.collider.GetComponent<Block>();
                if (hitBlock != null)
                {
                    detectedIsland = hitBlock.currentIsland;
                }
            }

            // 3. Strict Validation: Must be a different island, have a scale mismatch, and contain a valid tile at target position
            if (detectedIsland != null && detectedIsland != currentIsland && !mismatchedIslands.Contains(detectedIsland))
            {
                if (!currentIsland.HasSameScale(detectedIsland))
                {
                    Vector2Int landingPos = detectedIsland.WorldToGridPosition(targetWorldPos);

                    if (detectedIsland.IsValidLocalPos(landingPos))
                    {
                        mismatchedIslands.Add(detectedIsland);
                    }
                }
            }
        }

        return mismatchedIslands;
    }


    private void OnDrawGizmos()
    {
        if (currentIsland == null) return;


        Matrix4x4 oldMatrix = Gizmos.matrix;
        Vector3 myWorldPos = transform.position;

        Quaternion rotation = Quaternion.Euler(0, 0, currentIsland.transform.eulerAngles.z);
        Gizmos.matrix = Matrix4x4.TRS(myWorldPos, rotation, Vector3.one);

        Gizmos.matrix = oldMatrix;
    }
}