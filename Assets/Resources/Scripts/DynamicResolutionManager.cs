using UnityEngine;

public class DynamicResolutionManager : MonoBehaviour
{
    void Start()
    {
        // Scales world rendering resolution down to 75% to alleviate mobile GPU fill-rate strain
        ScalableBufferManager.ResizeBuffers(0.75f, 0.75f);
    }
}