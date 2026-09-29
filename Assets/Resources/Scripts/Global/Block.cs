using DG.Tweening;
using UnityEngine;

public abstract class Block : MonoBehaviour
{
    [Header("Base Block Properties")]
    public Vector2Int gridPosition;
    public GridIsland currentIsland;

    [Header("Visual References")]
    [SerializeField] protected SpriteRenderer spriteRenderer;
    [SerializeField] protected Color defaultColor = Color.white;
    [SerializeField] protected Color illegalColor = Color.red;


    [Header("Highlight Settings")]
    [SerializeField] internal Color highlightColor = new Color(1f, 1f, 0.5f, 1f); // Pale yellow
    private bool isCurrentlyHighlighted = false;
    public bool isBlockHighlighted => isCurrentlyHighlighted;

    private Tween shakeTween;
    private Tween colorTween;

    protected virtual void Awake()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (spriteRenderer != null)
            defaultColor = spriteRenderer.color;
    }

   

    public bool CanMoveTo(Vector2Int targetPos)
    {
        // Always query currentIsland dynamically!
        if (currentIsland == null) return false;

        // Check if the target position contains an existing block obstacle
        Block obstacle = currentIsland.GetBlockAtLocalPos(targetPos);
        if (obstacle != null && obstacle != this)
        {
            return false; // Cell occupied -> Prevent walking into/overlapping block!
        }

        return true;
    }

    /// <summary>
    /// Returns true if the target grid position contains a block that is both empty and highlighted.
    /// </summary>
    public bool IsTargetInHighlightedEmptyBlock(Vector2Int targetGridPos)
    {
        if (currentIsland == null) return false;

        // 1. Calculate the exact local and world position for the target grid coordinate
        int scaleX = Mathf.Max(1, currentIsland.scaleFactorX);
        int scaleY = Mathf.Max(1, currentIsland.scaleFactorY);

        Vector3 targetLocalPos = new Vector3(
            targetGridPos.x * scaleX,
            targetGridPos.y * scaleY,
            transform.localPosition.z
        );

        Vector3 targetWorldPos = transform.parent != null
            ? transform.parent.TransformPoint(targetLocalPos)
            : targetLocalPos;

        // 2. Find all colliders overlapping the target world position
        // (Ensure checkSize matches the physical size of your blocks)
        Collider2D[] hits = Physics2D.OverlapBoxAll(targetWorldPos, new Vector2(2,2), 0f);

        foreach (var hit in hits)
        {
            // Try to get a Block component from the overlapped object
            Block hitBlock = hit.GetComponent<Block>();

            // Ensure we found a block, and that we aren't checking OURSELVES
            if (hitBlock != null && hitBlock.gameObject != this.gameObject)
            {
                // 3. Check if the block is highlighted 
                // NOTE: If you have a specific boolean or tag for "Empty Block" (like public bool isEmpty), add it here!
                if (hitBlock.isCurrentlyHighlighted /* && hitBlock.isEmpty */)
                {
                    return true; // Found a highlighted empty block at the destination
                }
            }
        }

        return false; // No highlighted empty block found at destination
    }

    /// <summary>
    /// Can this block be selected and swiped directly by the player?
    /// </summary>
    public abstract bool CanPlayerMoveDirectly();

    /// <summary>
    /// Does this block fall under gravity when unsupported?
    /// </summary>
    public abstract bool IsAffectedByGravity();

    public virtual void MoveToGridPosition(Vector2Int newLocalPos, float duration = 0.2f)
    {
        gridPosition = newLocalPos;

        if (currentIsland != null)
        {
            // Calculate scaled local position within island using scaleFactorX/Y
            int scaleX = Mathf.Max(1, currentIsland.scaleFactorX);
            int scaleY = Mathf.Max(1, currentIsland.scaleFactorY);

            Vector3 targetLocalPos = new Vector3(
                newLocalPos.x * scaleX,
                newLocalPos.y * scaleY,
                transform.localPosition.z
            );

            // Animate local move relative to parent island
            transform.DOLocalMove(targetLocalPos, duration).SetEase(Ease.OutQuad);
        }
    }

    public virtual void PlayIllegalMoveAnimation(Vector2 swipeDirection)
    {
        shakeTween?.Kill();
        colorTween?.Kill();

        Vector3 initialPos = transform.position;
        Vector3 nudgeOffset = (Vector3)swipeDirection.normalized * 0.15f;

        shakeTween = transform.DOPunchPosition(nudgeOffset, 0.25f, vibrato: 10, elasticity: 0.5f)
            .OnComplete(() => transform.position = initialPos);

        if (spriteRenderer != null)
        {
            spriteRenderer.color = illegalColor;
            colorTween = spriteRenderer.DOColor(defaultColor, 0.3f);
        }
    }

    /// <summary>
    /// Checks if the current block overlaps with another Block that is empty and highlighted.
    /// </summary>
    /// <summary>
    /// Checks if this block's BoxCollider2D overlaps with another empty and highlighted Block.
    /// </summary>
    /// <summary>
    /// Checks if the block at the given target local grid coordinate is highlighted.
    /// </summary>
    /// <param name="targetLocalPos">The target grid coordinate to inspect.</param>
    /// <returns>True if a block exists at the target position and is currently highlighted.</returns>
    public bool IsBlockHighlightedAtTarget(Vector2Int targetLocalPos)
    {
        if (currentIsland == null) return false;

        int scaleX = Mathf.Max(1, currentIsland.scaleFactorX);
        int scaleY = Mathf.Max(1, currentIsland.scaleFactorY);

        Vector3 targetLocal = new Vector3(
            targetLocalPos.x * scaleX,
            targetLocalPos.y * scaleY,
            transform.localPosition.z
        );

        Vector3 targetWorldPos = transform.parent != null
            ? transform.parent.TransformPoint(targetLocal)
            : targetLocal;

        // 1. Calculate box size dynamically from BoxCollider2D
        BoxCollider2D boxCollider = GetComponent<BoxCollider2D>();
        Vector2 boxSize = boxCollider.size * (Vector2)transform.lossyScale;

        // 2. Perform overlap check at target position using exact collider dimensions
        RaycastHit2D[] hits = Physics2D.RaycastAll(transform.position, (targetWorldPos-transform.position).normalized, Vector2.Distance(targetWorldPos,transform.position));

        foreach (var hit in hits)
        {
            if (hit.collider.gameObject == gameObject || hit.transform.IsChildOf(transform)) continue;
            Debug.DrawLine(targetWorldPos, transform.position);
            if (hit.collider.TryGetComponent<Block>(out Block targetBlock))
            {
                if (targetBlock.isCurrentlyHighlighted)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Checks if the player is at this block's current grid coordinate and applies the highlight.
    /// </summary>


    private void OnTriggerEnter2D(Collider2D collision)
    {
        if(collision.CompareTag("Player"))
        {
            SetHighlight(true);
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            SetHighlight(false);
        }
    }
    private void SetHighlight(bool enable)
    {
        isCurrentlyHighlighted = enable;

        if (spriteRenderer != null)
        {
            // Smoothly tween the color change (Requires DOTween)
            spriteRenderer.DOColor(enable ? highlightColor : defaultColor, 0.2f);

            // Alternatively, for an instant color snap:
            // spriteRenderer.color = enable ? highlightColor : originalColor;
        }
    }
}