using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ImmovableBlock : Block
{
    [Header("Visual Feedback References")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color highlightColor = Color.yellow;
    [SerializeField] private Color blinkColor = Color.cyan;

    private Coroutine blinkCoroutine;

    public override bool CanPlayerMoveDirectly() => false;
    public override bool IsAffectedByGravity() => false;

    private void Awake()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null) normalColor = spriteRenderer.color;
    }

    /// <summary>
    /// Returns all orthogonally adjacent Joint Blocks.
    /// </summary>
    public List<JointBlock> GetAdjacentJointBlocks()
    {
        List<JointBlock> joints = new List<JointBlock>();
        if (currentIsland == null) return joints;

        Vector2Int[] adjacentDirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        foreach (var dir in adjacentDirs)
        {
            Vector2Int neighborPos = gridPosition + dir;
            if (currentIsland.IsValidLocalPos(neighborPos))
            {
                Block neighbor = currentIsland.GetBlockAtLocalPos(neighborPos);
                if (neighbor is JointBlock joint)
                {
                    joints.Add(joint);
                }
            }
        }

        return joints;
    }

    public void SetBlinkingState(bool enable)
    {
        if (blinkCoroutine != null)
        {
            StopCoroutine(blinkCoroutine);
            blinkCoroutine = null;
        }

        if (enable)
        {
            blinkCoroutine = StartCoroutine(BlinkRoutine());
        }
        else
        {
            if (spriteRenderer != null) spriteRenderer.color = normalColor;
        }
    }

    public void SetLinkedHighlight(bool enable)
    {
        if (blinkCoroutine != null)
        {
            StopCoroutine(blinkCoroutine);
            blinkCoroutine = null;
        }

        if (spriteRenderer != null)
        {
            spriteRenderer.color = enable ? highlightColor : normalColor;
        }
    }

    private IEnumerator BlinkRoutine()
    {
        while (true)
        {
            if (spriteRenderer != null)
                spriteRenderer.color = blinkColor;
            yield return new WaitForSeconds(0.3f);

            if (spriteRenderer != null)
                spriteRenderer.color = normalColor;
            yield return new WaitForSeconds(0.3f);
        }
    }
}