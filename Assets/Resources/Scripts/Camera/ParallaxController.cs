using System.Collections.Generic;
using UnityEngine;

public class ParallaxController : MonoBehaviour
{
    [System.Serializable]
    public class ParallaxLayer
    {
        public GameObject layerObject;

        [Range(-2f, 2f)]
        public float horizontalStrength;

        [Range(-2f, 2f)]
        public float verticalStrength;

        [Header("Absolute World Space Bounds")]
        public bool useHorizontalBounds;
        public Vector2 xWorldMinMax;

        [Header("Absolute World Space Bounds")]
        public bool useVerticalBounds;
        public Vector2 yWorldMinMax;

        [Header("Trigger Execution Rules")]
        [Tooltip("If true, this object stays perfectly fixed in the world, and ONLY starts its parallax movement when its bounds overlap the camera view.")]
        public bool parallaxOnlyWhenVisible;

        // Cached reference to the object's visual bounds
        [HideInInspector] public Renderer cachedRenderer;
        [HideInInspector] public Collider2D cachedCollider;
        [HideInInspector] public bool componentsChecked;
    }

    public List<ParallaxLayer> layers;
    private Camera cam;
    private Vector3 lastCameraPosition;
    private bool isFirstFrame = true;

    void Start()
    {
        cam = GetComponent<Camera>();
        if (cam == null) cam = Camera.main;

        lastCameraPosition = transform.position;

        // Pre-cache bounds components to optimize runtime calculation loops
        foreach (var layer in layers)
        {
            CacheLayerComponents(layer);
        }
    }

    void LateUpdate()
    {
        Vector3 currentCameraPosition = transform.position;

        if (isFirstFrame)
        {
            lastCameraPosition = currentCameraPosition;
            isFirstFrame = false;
            return;
        }

        Vector3 deltaMovement = currentCameraPosition - lastCameraPosition;
        Bounds cameraBounds = GetCameraWorldBounds();

        foreach (var layer in layers)
        {
            if (layer.layerObject == null) continue;

            Vector3 currentPos = layer.layerObject.transform.position;

            float moveX = 0f;
            float moveY = 0f;

            if (layer.parallaxOnlyWhenVisible)
            {
                // 1. Get the current bounding box of the layer object
                Bounds layerBounds = GetLayerWorldBounds(layer);

                // 2. Perform a true AABB Intersection test (Checks if any part of the bounds overlap)
                bool isOverlapping = cameraBounds.Intersects(layerBounds);

                if (isOverlapping)
                {
                    // OVERLAPPING: Apply the active parallax drift effect
                    moveX = deltaMovement.x * layer.horizontalStrength;
                    moveY = deltaMovement.y * layer.verticalStrength;
                }
                else
                {
                    // NO OVERLAP: Keep it completely still in world coordinates
                    continue;
                }
            }
            else
            {
                // Standard Global Parallax Layer
                moveX = deltaMovement.x * layer.horizontalStrength;
                moveY = deltaMovement.y * layer.verticalStrength;
            }

            // 3. Predict absolute world positions
            float targetX = currentPos.x + moveX;
            float targetY = currentPos.y + moveY;

            // 4. Enforce rigid world boundaries if required
            if (layer.useHorizontalBounds)
            {
                targetX = Mathf.Clamp(targetX, layer.xWorldMinMax.x, layer.xWorldMinMax.y);
            }

            if (layer.useVerticalBounds)
            {
                targetY = Mathf.Clamp(targetY, layer.yWorldMinMax.x, layer.yWorldMinMax.y);
            }

            layer.layerObject.transform.position = new Vector3(targetX, targetY, currentPos.z);
        }

        lastCameraPosition = currentCameraPosition;
    }

    private void CacheLayerComponents(ParallaxLayer layer)
    {
        if (layer.layerObject == null || layer.componentsChecked) return;

        layer.cachedRenderer = layer.layerObject.GetComponent<Renderer>();
        if (layer.cachedRenderer == null)
        {
            layer.cachedRenderer = layer.layerObject.GetComponentInChildren<Renderer>();
        }

        layer.cachedCollider = layer.layerObject.GetComponent<Collider2D>();
        if (layer.cachedCollider == null)
        {
            layer.cachedCollider = layer.layerObject.GetComponentInChildren<Collider2D>();
        }

        layer.componentsChecked = true;
    }

    /// <summary>
    /// Gets the absolute bounding box area of the layer object using its SpriteRenderer or Collider2D.
    /// Falls back safely to a default point if no visual bounding component is detected.
    /// </summary>
    private Bounds GetLayerWorldBounds(ParallaxLayer layer)
    {
        // Make sure components are cached (safeguards dynamically added elements)
        if (!layer.componentsChecked) CacheLayerComponents(layer);

        if (layer.cachedRenderer != null) return layer.cachedRenderer.bounds;
        if (layer.cachedCollider != null) return layer.cachedCollider.bounds;

        // Fallback to pivot point if no spatial components exist
        return new Bounds(layer.layerObject.transform.position, Vector3.one * 0.1f);
    }

    private Bounds GetCameraWorldBounds()
    {
        if (cam == null) return new Bounds(transform.position, Vector3.zero);

        float screenAspect = (float)Screen.width / Screen.height;
        float cameraHeight = cam.orthographicSize * 2f;
        float cameraWidth = cameraHeight * screenAspect;

        return new Bounds(cam.transform.position, new Vector3(cameraWidth, cameraHeight, 20f));
    }
}