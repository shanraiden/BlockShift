using System.Collections.Generic;
using System.Linq;
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

        [Header("Trigger & Bounding Box Rules")]
        [Tooltip("If checked, parallax only happens when the camera rectangle and layer rectangle touch or overlap.")]
        public bool parallaxOnlyWhenVisible;

        [Tooltip("Center offset relative to the layer object's position.")]
        public Vector2 boundsCenterOffset = Vector2.zero;

        [Tooltip("Fallback size used if no SpriteRenderer is detected.")]
        public Vector2 fallbackBoundsSize = new Vector2(40f, 20f);

        [Header("Absolute World Space Clamp Bounds")]
        public bool useHorizontalBounds;
        public Vector2 xWorldMinMax;

        [Header("Absolute World Space Clamp Bounds")]
        public bool useVerticalBounds;
        public Vector2 yWorldMinMax;
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
    }

    void LateUpdate()
    {
        if (cam == null) return;

        Vector3 currentCameraPosition = transform.position;

        if (isFirstFrame)
        {
            lastCameraPosition = currentCameraPosition;
            isFirstFrame = false;
            return;
        }

        Vector3 deltaMovement = currentCameraPosition - lastCameraPosition;
        Bounds cameraBounds = GetCameraWorldBounds(cam);

        foreach (var layer in layers)
        {
            if (layer.layerObject == null) continue;
            if (layer.layerObject.transform.childCount == 0)
            {
                initiateParallexing(layer.layerObject, layer, cameraBounds, deltaMovement);
            }
            else
            {
                GameObject[] childObjects = layer.layerObject.GetComponentsInChildren<Transform>()
                                                           .Where(t => t != layer.layerObject.transform)
                                                           .Select(t => t.gameObject)
                                                           .ToArray();

                foreach (GameObject childObject in childObjects)
                {
                    print(childObject.name);
                    initiateParallexing(childObject, layer, cameraBounds, deltaMovement);

                }
            }
            
        }

        lastCameraPosition = currentCameraPosition;
    }

    private void initiateParallexing(GameObject layerObject,ParallaxLayer layer,Bounds cameraBounds,Vector3 deltaMovement)
    { 
    Vector3 currentPos = layerObject.transform.position;
            float moveX = 0f;
            float moveY = 0f;

            if (layer.parallaxOnlyWhenVisible)
            {
                Bounds layerBounds = GetLayerWorldBounds(layerObject);
                bool isTouchingOrOverlapping = Intersects2D(cameraBounds, layerBounds);

                if (isTouchingOrOverlapping)
                {
                    moveX = deltaMovement.x * layer.horizontalStrength;
                    moveY = deltaMovement.y * layer.verticalStrength;
                }
              
            }
            else
            {
                moveX = deltaMovement.x * layer.horizontalStrength;
                moveY = deltaMovement.y * layer.verticalStrength;
            }

            float targetX = currentPos.x + moveX;
            float targetY = currentPos.y + moveY;

            if (layer.useHorizontalBounds)
            {
                targetX = Mathf.Clamp(targetX, layer.xWorldMinMax.x, layer.xWorldMinMax.y);
            }

            if (layer.useVerticalBounds)
            {
                targetY = Mathf.Clamp(targetY, layer.yWorldMinMax.x, layer.yWorldMinMax.y);
            }

        layerObject.transform.position = new Vector3(targetX, targetY, currentPos.z);
    }
    /// <summary>
    /// Automatically calculates precise world bounds based on SpriteRenderer DrawMode (Simple vs Tiled/Sliced).
    /// </summary>
    private Bounds GetLayerWorldBounds(GameObject layer)
    {
        SpriteRenderer sr = layer.GetComponent<SpriteRenderer>();
        if (sr == null) sr = layer.GetComponentInChildren<SpriteRenderer>();

        Vector2 size = Vector2.zero;

        if (sr != null && sr.sprite != null)
        {
            if (sr.drawMode == SpriteDrawMode.Simple)
            {
                // Simple mode uses the native dimensions of the sprite asset
                size = sr.sprite.rect.size / sr.sprite.pixelsPerUnit;
            }
            else if (sr.drawMode == SpriteDrawMode.Tiled || sr.drawMode == SpriteDrawMode.Sliced)
            {
                // Tiled/Sliced mode uses the explicitly defined size field on the renderer
                size = sr.size;
            }

            // Factor in world scale scaling
            Vector3 lossyScale = sr.transform.lossyScale;
            size.x *= Mathf.Abs(lossyScale.x);
            size.y *= Mathf.Abs(lossyScale.y);
        }

        Vector3 worldCenter = layer.transform.position ;
        return new Bounds(worldCenter, new Vector3(size.x, size.y, 999f));
    }

    private Bounds GetCameraWorldBounds(Camera targetCam)
    {
        float cameraHeight = targetCam.orthographicSize * 2f;
        float cameraWidth = cameraHeight * targetCam.aspect;
        return new Bounds(targetCam.transform.position, new Vector3(cameraWidth, cameraHeight, 50f));
    }

    private bool Intersects2D(Bounds a, Bounds b)
    {
        return (a.min.x <= b.max.x && a.max.x >= b.min.x) &&
               (a.min.y <= b.max.y && a.max.y >= b.min.y);
    }

}