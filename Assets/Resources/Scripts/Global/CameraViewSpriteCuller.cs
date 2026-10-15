using UnityEngine;

public class CameraViewSpriteCuller : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Extra margin around the camera view so sprites don't pop in/out abruptly.")]
    public float viewBuffer = 5f;
    [Tooltip("How often (in seconds) to check visibility to save CPU performance.")]
    public float checkInterval = 0.2f;

    private Camera mainCam;
    private SpriteRenderer[] allSpriteRenderers;
    private Plane[] frustumPlanes;
    private float timer;

    void Start()
    {
        mainCam = Camera.main;

        // Find all sprite renderers in the scene
        allSpriteRenderers = FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None);
        frustumPlanes = new Plane[6];
    }

    void Update()
    {
        if (mainCam == null) return;

        timer += Time.deltaTime;
        if (timer >= checkInterval)
        {
            timer = 0f;

            // Extract the 6 planes of the camera's view frustum
            GeometryUtility.CalculateFrustumPlanes(mainCam, frustumPlanes);

            for (int i = 0; i < allSpriteRenderers.Length; i++)
            {
                if (allSpriteRenderers[i] == null) continue;

                Bounds spriteBounds = allSpriteRenderers[i].bounds;

                // Expand bounds slightly by the buffer value to account for wave vertex displacement 
                // so animated water edges don't clip prematurely.
                spriteBounds.Expand(viewBuffer);

                // Test if the sprite's bounding box intersects with the camera's view planes
                bool isVisible = GeometryUtility.TestPlanesAABB(frustumPlanes, spriteBounds);

                // Enable or disable renderer based on camera view intersection
                if (allSpriteRenderers[i].enabled != isVisible)
                {
                    allSpriteRenderers[i].enabled = isVisible;
                }
            }
        }
    }
}