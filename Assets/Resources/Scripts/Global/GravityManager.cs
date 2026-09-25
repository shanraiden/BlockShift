using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class GravityManager : MonoBehaviour
{
    [SerializeField] private float fallStepDuration = 0.15f;
    [SerializeField] private Ease fallEase = Ease.InQuad;

    public IEnumerator ApplyGravityRoutine(List<GridIsland> activeIslands)
    {
        if (activeIslands == null || activeIslands.Count == 0)
        {
            yield break;
        }

        bool blockFellThisStep;
        int passCount = 0;

        do
        {
            blockFellThisStep = false;
            passCount++;
            List<Tween> activeFallTweens = new List<Tween>();

            foreach (var island in activeIslands)
            {
                if (island == null) continue;

                // Extract island integer scale factors
                int scaleX = Mathf.Max(1, island.scaleFactorX);
                int scaleY = Mathf.Max(1, island.scaleFactorY);

                // Scan bottom-to-top starting from y = 1 up to height - 1
                for (int y = 1; y < island.height; y++)
                {
                    for (int x = 0; x < island.width; x++)
                    {
                        Vector2Int currentPos = new Vector2Int(x, y);
                        Block block = island.GetBlockAtLocalPos(currentPos);

                        if (block != null)
                        {
                            bool isDynamic = block.IsAffectedByGravity();
                            Vector2Int belowPos = currentPos + Vector2Int.down;
                            bool isValidBelow = island.IsValidLocalPos(belowPos);
                            bool isOccupiedBelow = island.IsCellOccupiedLocal(belowPos);

                            if (isDynamic && isValidBelow && !isOccupiedBelow)
                            {
                                // Update Grid Array Data
                                island.ExecuteMoveLocal(currentPos, belowPos);
                                block.gridPosition = belowPos;

                                // Scaled local target position within island parent space
                                Vector3 targetLocalPos = new Vector3(
                                    belowPos.x * scaleX,
                                    belowPos.y * scaleY,
                                    block.transform.localPosition.z
                                );

                                Tween fallTween = block.transform
                                    .DOLocalMove(targetLocalPos, fallStepDuration)
                                    .SetEase(fallEase);

                                activeFallTweens.Add(fallTween);
                                blockFellThisStep = true;
                            }
                        }
                    }
                }
            }

            if (activeFallTweens.Count > 0)
            {
                yield return new WaitForSeconds(fallStepDuration);
            }

        } while (blockFellThisStep);
    }
}