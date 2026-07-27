using UnityEngine;

public class MovableStaticBlock : Block
{
    public override bool CanPlayerMoveDirectly() => true;
    public override bool IsAffectedByGravity() => false;
}