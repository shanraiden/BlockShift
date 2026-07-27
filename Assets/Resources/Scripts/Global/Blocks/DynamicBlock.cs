using UnityEngine;

public class DynamicBlock : Block
{
    public override bool CanPlayerMoveDirectly() => true;
    public override bool IsAffectedByGravity() => true;
}