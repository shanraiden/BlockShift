using UnityEngine;

public class ImmovableBlock : Block
{
    public override bool CanPlayerMoveDirectly() => false;
    public override bool IsAffectedByGravity() => false;
}