using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

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

        [Tooltip("Center offset relative to the resolved layer bounds center.")]
        public Vector2 boundsCenterOffset = Vector2.zero;

        [Tooltip("Fallback size used if no Renderer / Tilemap is detected.")]
        public Vector2 fallbackBoundsSize = new Vector2(40f, 20f);

        [Header("Absolute World Space Clamp Bounds")]
        public bool useHorizontalBounds;
        public Vector2 xWorldMinMax;

        [Header("Absolute World Space Clamp Bounds")]
        public bool useVerticalBounds;
        public Vector2 yWorldMinMax;
    }

    public List<ParallaxLayer> layers = new List<ParallaxLayer>();
    private Camera cam;
    private Vector3 lastCameraPosition;
    private bool isFirstFrame = true;

    private void Start()
    {
        cam = GetComponent<Camera>();
        if (cam == null) cam = Camera.main;

        CompressAllLayerTilemaps();

        if (cam != null)
        {
            lastCameraPosition = cam.transform.position;
        }
    }

    private void CompressAllLayerTilemaps()
    {
        if (layers == null) return;

        for (int i = 0; i < layers.Count; i++)
        {
            ParallaxLayer layer = layers[i];
            if (layer == null || layer.layerObject == null) continue;

            Tilemap[] tilemaps = layer.layerObject.GetComponentsInChildren<Tilemap>(true);
            for (int t = 0; t < tilemaps.Length; t++)
            {
                if (tilemaps[t] != null)
                {
                    tilemaps[t].CompressBounds();
                }
            }
        }
    }

    private void LateUpdate()
    {
        if (cam == null)
        {
            cam = GetComponent<Camera>();
            if (cam == null) cam = Camera.main;
            if (cam == null) return;
        }

        Vector3 currentCameraPosition = cam.transform.position;

        if (isFirstFrame)
        {
            lastCameraPosition = currentCameraPosition;
            isFirstFrame = false;
            return;
        }

        Vector3 deltaMovement = currentCameraPosition - lastCameraPosition;
        Bounds cameraBounds = GetCameraWorldBounds(cam);

        if (layers != null)
        {
            for (int i = 0; i < layers.Count; i++)
            {
                ParallaxLayer layer = layers[i];
                if (layer == null || layer.layerObject == null) continue;

                // If the layerObject itself is a SpriteRenderer/Tilemap/Renderer OR has no children, parallax it directly.
                // If it's an empty container with multiple child sprites/tilemaps, parallax each direct child individually.
                bool hasOwnRenderer = layer.layerObject.GetComponent<Renderer>() != null ||
                                      layer.layerObject.GetComponent<Tilemap>() != null;

                Transform layerTf = layer.layerObject.transform;
                if (hasOwnRenderer || layerTf.childCount == 0)
                {
                    InitiateParallaxing(layer.layerObject, layer, cameraBounds, deltaMovement);
                }
                else
                {
                    int childCount = layerTf.childCount;
                    for (int c = 0; c < childCount; c++)
                    {
                        GameObject childObject = layerTf.GetChild(c).gameObject;
                        InitiateParallaxing(childObject, layer, cameraBounds, deltaMovement);
                    }
                }
            }
        }

        lastCameraPosition = currentCameraPosition;
    }

    private void InitiateParallaxing(GameObject targetObject, ParallaxLayer layer, Bounds cameraBounds, Vector3 deltaMovement)
    {
        if (targetObject == null) return;

        Vector3 currentPos = targetObject.transform.position;
        float moveX = 0f;
        float moveY = 0f;

        if (layer.parallaxOnlyWhenVisible)
        {
            Bounds layerBounds = GetLayerWorldBounds(targetObject, layer);
            if (Intersects2D(cameraBounds, layerBounds))
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

        targetObject.transform.position = new Vector3(targetX, targetY, currentPos.z);
    }

    /// <summary>
    /// Calculates accurate world bounds for Tilemaps, SpriteRenderers (Simple/Tiled/Sliced),
    /// or any nested Renderers, falling back to layer.fallbackBoundsSize if none exist.
    /// </summary>
    private Bounds GetLayerWorldBounds(GameObject targetObject, ParallaxLayer layer)
    {
        bool hasBounds = false;
        Bounds combinedBounds = default;

        // 1. Check Tilemap + TilemapRenderer first (accurate even when tiles are painted far from Transform origin)
        Tilemap[] tilemaps = targetObject.GetComponentsInChildren<Tilemap>(false);
        for (int i = 0; i < tilemaps.Length; i++)
        {
            Tilemap tm = tilemaps[i];
            if (tm == null) continue;

            if (TryGetTilemapWorldBounds(tm, out Bounds tmBounds))
            {
                if (!hasBounds)
                {
                    combinedBounds = tmBounds;
                    hasBounds = true;
                }
                else
                {
                    combinedBounds.Encapsulate(tmBounds);
                }
            }
        }

        // 2. Check SpriteRenderers & any other Renderers (skipping TilemapRenderers already handled above)
        Renderer[] renderers = targetObject.GetComponentsInChildren<Renderer>(false);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer rend = renderers[i];
            if (rend == null || !rend.enabled || rend is TilemapRenderer)
                continue;

            Bounds rendBounds = GetSingleRendererWorldBounds(rend);
            if (rendBounds.size.x <= 0.0001f && rendBounds.size.y <= 0.0001f)
                continue;

            if (!hasBounds)
            {
                combinedBounds = rendBounds;
                hasBounds = true;
            }
            else
            {
                combinedBounds.Encapsulate(rendBounds);
            }
        }

        if (!hasBounds)
        {
            combinedBounds = new Bounds(
                targetObject.transform.position,
                new Vector3(Mathf.Max(0.1f, layer.fallbackBoundsSize.x), Mathf.Max(0.1f, layer.fallbackBoundsSize.y), 999f)
            );
        }

        combinedBounds.center += new Vector3(layer.boundsCenterOffset.x, layer.boundsCenterOffset.y, 0f);
        combinedBounds.size = new Vector3(combinedBounds.size.x, combinedBounds.size.y, 999f);
        return combinedBounds;
    }

    private static bool TryGetTilemapWorldBounds(Tilemap tilemap, out Bounds worldBounds)
    {
        worldBounds = default;

        TilemapRenderer tr = tilemap.GetComponent<TilemapRenderer>();
        if (tr != null && !tr.enabled)
            return false;

        BoundsInt cellBounds = tilemap.cellBounds;
        if (cellBounds.size.x <= 0 || cellBounds.size.y <= 0)
            return false;

        // If TilemapRenderer already has valid non-zero world bounds, use them directly
        if (tr != null && (tr.bounds.size.x > 0.001f || tr.bounds.size.y > 0.001f))
        {
            worldBounds = tr.bounds;
            return true;
        }

        // Fallback: compute world AABB from tilemap's localBounds corners
        Bounds localBounds = tilemap.localBounds;
        if (localBounds.size.x <= 0.001f && localBounds.size.y <= 0.001f)
            return false;

        Transform tmTf = tilemap.transform;
        Vector3 min = localBounds.min;
        Vector3 max = localBounds.max;

        Vector3 c0 = tmTf.TransformPoint(new Vector3(min.x, min.y, 0f));
        Vector3 c1 = tmTf.TransformPoint(new Vector3(max.x, min.y, 0f));
        Vector3 c2 = tmTf.TransformPoint(new Vector3(min.x, max.y, 0f));
        Vector3 c3 = tmTf.TransformPoint(new Vector3(max.x, max.y, 0f));

        worldBounds = new Bounds(c0, Vector3.zero);
        worldBounds.Encapsulate(c1);
        worldBounds.Encapsulate(c2);
        worldBounds.Encapsulate(c3);
        return true;
    }

    private static Bounds GetSingleRendererWorldBounds(Renderer rend)
    {
        if (rend is SpriteRenderer sr && sr.sprite != null)
        {
            if (sr.drawMode == SpriteDrawMode.Tiled || sr.drawMode == SpriteDrawMode.Sliced)
            {
                Vector3 lossyScale = sr.transform.lossyScale;
                Vector2 size = new Vector2(
                    Mathf.Abs(sr.size.x * lossyScale.x),
                    Mathf.Abs(sr.size.y * lossyScale.y)
                );
                return new Bounds(sr.bounds.center, new Vector3(size.x, size.y, 999f));
            }

            return sr.bounds;
        }

        return rend.bounds;
    }

    private Bounds GetCameraWorldBounds(Camera targetCam)
    {
        if (targetCam.orthographic)
        {
            float cameraHeight = targetCam.orthographicSize * 2f;
            float cameraWidth = cameraHeight * targetCam.aspect;
            return new Bounds(targetCam.transform.position, new Vector3(cameraWidth, cameraHeight, 999f));
        }

        float dist = Mathf.Abs(targetCam.transform.position.z);
        float frustumHeight = 2f * dist * Mathf.Tan(targetCam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float frustumWidth = frustumHeight * targetCam.aspect;
        return new Bounds(targetCam.transform.position, new Vector3(frustumWidth, frustumHeight, 999f));
    }

    private static bool Intersects2D(Bounds a, Bounds b)
    {
        return (a.min.x <= b.max.x && a.max.x >= b.min.x) &&
               (a.min.y <= b.max.y && a.max.y >= b.min.y);
    }

    private void OnDrawGizmosSelected()
    {
        Camera targetCam = cam != null ? cam : GetComponent<Camera>();
        if (targetCam == null) targetCam = Camera.main;
        if (targetCam == null || layers == null) return;

        Bounds camBounds = GetCameraWorldBounds(targetCam);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(new Vector3(camBounds.center.x, camBounds.center.y, 0f), new Vector3(camBounds.size.x, camBounds.size.y, 0.1f));

        for (int i = 0; i < layers.Count; i++)
        {
            ParallaxLayer layer = layers[i];
            if (layer == null || layer.layerObject == null || !layer.parallaxOnlyWhenVisible)
                continue;

            bool hasOwnRenderer = layer.layerObject.GetComponent<Renderer>() != null ||
                                  layer.layerObject.GetComponent<Tilemap>() != null;
            Transform layerTf = layer.layerObject.transform;

            if (hasOwnRenderer || layerTf.childCount == 0)
            {
                DrawLayerBoundsGizmo(layer.layerObject, layer, camBounds);
            }
            else
            {
                for (int c = 0; c < layerTf.childCount; c++)
                {
                    DrawLayerBoundsGizmo(layerTf.GetChild(c).gameObject, layer, camBounds);
                }
            }
        }
    }

    private void DrawLayerBoundsGizmo(GameObject targetObj, ParallaxLayer layer, Bounds camBounds)
    {
        Bounds b = GetLayerWorldBounds(targetObj, layer);
        Gizmos.color = Intersects2D(camBounds, b) ? Color.green : new Color(1f, 0.4f, 0.2f, 0.75f);
        Gizmos.DrawWireCube(new Vector3(b.center.x, b.center.y, 0f), new Vector3(b.size.x, b.size.y, 0.1f));
    }
}