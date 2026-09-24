using UnityEngine;

public class DynamicFpsManager : MonoBehaviour
{
    private void Awake()
    {
        // 1. Disable VSync so Application.targetFrameRate takes effect
        QualitySettings.vSyncCount = 0;

        // 2. Convert refreshRateRatio (double) to int using Mathf.RoundToInt
        double refreshRateDouble = Screen.currentResolution.refreshRateRatio.value;
        int targetFPS = Mathf.RoundToInt((float)refreshRateDouble);

        // 3. Fallback check for platforms returning 0 or invalid values
        if (targetFPS <= 0)
        {
            targetFPS = 60;
        }

        // 4. Apply to targetFrameRate
        Application.targetFrameRate = targetFPS;

        Debug.Log($"[FPSManager] Target frame rate set to: {Application.targetFrameRate} FPS");
    }


}