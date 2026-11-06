using UnityEngine;

public class DynamicFpsManager : MonoBehaviour
{
    private void Awake()
    {
        Application.targetFrameRate = Mathf.RoundToInt((float)Screen.currentResolution.refreshRateRatio.value); QualitySettings.vSyncCount = 0;

    }


}