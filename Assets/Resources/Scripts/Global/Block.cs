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
            Vector3 targetWorldPos = new Vector3(
                currentIsland.originPosition.x + newLocalPos.x,
                currentIsland.originPosition.y + newLocalPos.y,
                0
            );

            transform.DOMove(targetWorldPos, duration).SetEase(Ease.OutQuad);
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
}