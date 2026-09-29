using UnityEngine;

public class EmptyBlock : Block
{
    public override bool CanPlayerMoveDirectly()
    {
        return false; // Empty blocks cannot be moved directly by the player
    }

    public override bool IsAffectedByGravity()
    {
        return false; // Empty blocks are not affected by gravity
    }

    
}
