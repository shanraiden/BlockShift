using System.Collections.Generic;
using UnityEngine;

public class CameraController : MonoBehaviour
{
    [SerializeField] private Camera cam;
    [SerializeField] private float padding = 1.5f; // Extra border space around grid edges

    private void Awake()
    {
        if (cam == null) cam = GetComponent<Camera>();
    }

    /// <summary>
    /// Calculates the global bounding box for all islands in a level
    /// and adjusts camera position and orthographic size to fit them all.
    /// </summary>
    public void AdjustCameraToMultiGrid(List<GridIslandData> islands)
    {
        if (islands == null || islands.Count == 0) return;
        if (cam == null) cam = Camera.main;

        float minX = float.MaxValue;
        float maxX = float.MinValue;
        float minY = float.MaxValue;
        float maxY = float.MinValue;

        // 1. Calculate the combined world bounds across all islands
        foreach (var island in islands)
        {
            float islandMinX = island.originPosition.x - 0.5f;
            float islandMaxX = island.originPosition.x + island.width - 0.5f;
            float islandMinY = island.originPosition.y - 0.5f;
            float islandMaxY = island.originPosition.y + island.height - 0.5f;

            if (islandMinX < minX) minX = islandMinX;
            if (islandMaxX > maxX) maxX = islandMaxX;
            if (islandMinY < minY) minY = islandMinY;
            if (islandMaxY > maxY) maxY = islandMaxY;
        }

        // 2. Find the center of all islands
        float boundsWidth = maxX - minX;
        float boundsHeight = maxY - minY;

        float centerX = minX + (boundsWidth / 2f);
        float centerY = minY + (boundsHeight / 2f);

        // Center camera position over combined bounds while keeping Z depth at -10
        transform.position = new Vector3(centerX, centerY, -10f);

        // 3. Calculate orthographic size to fit both height and width
        float aspect = (float)Screen.width / Screen.height;

        float verticalSize = (boundsHeight / 2f) + padding;
        float horizontalSize = ((boundsWidth / 2f) + padding) / aspect;

        cam.orthographic = true;
        cam.orthographicSize = Mathf.Max(verticalSize, horizontalSize);
    }

    /// <summary>
    /// Legacy fallback for single-grid levels.
    /// </summary>
  
}