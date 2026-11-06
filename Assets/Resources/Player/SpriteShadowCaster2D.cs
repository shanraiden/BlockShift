using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class SpriteShadowCaster2D : MonoBehaviour
{
    public enum ShadowType { BlackSolid, WhiteEthereal }

    [Header("Projection Settings")]
    public Vector2 baseLightDirection = new Vector2(0f, -1f); // Neutral vertical bias
    public float shadowLength = 2f;

    [Header("Transparency / Alpha Settings")]
    [Tooltip("Pixels with alpha at or below this threshold are treated as transparent and will not cast shadows.")]
    [Range(0.01f, 1f)]
    public float alphaThreshold = 0.1f;

    [Tooltip("Maximum grid resolution used to sample non-transparent pixels from the sprite.")]
    [Range(16, 256)]
    public int maxPixelSampleResolution = 128;

    [Header("Width Scaling Settings")]
    [Range(0.1f, 3f)]
    public float shadowWidthFactor = 1.0f;

    [Header("Player Interaction")]
    public bool affectedByPlayerPosition = true;
    public bool shadowDirectionPull;
    [Range(0f, 1f)]
    public float playerRepulsionStrength = 1.0f;
    private Transform playerTransform;

    [Header("Dynamic Scaling Settings")]
    public float minShadowLength = 1f;
    public float maxShadowLength = 5f;
    public float maxInteractionDistance = 6f;

    [Header("Smoothing Settings")]
    public float transitionSpeed = 10f;

    [Header("Solidification Settings")]
    public ShadowType currentShadowType = ShadowType.BlackSolid;
    public Material blackShadowMaterial;
    public Material whiteShadowMaterial;

    // Component and Runtime References
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Mesh currentMesh;
    private SpriteRenderer spriteRenderer;

    private Vector2 currentShadowDir;
    private float currentLength;
    private bool isInPlayerRange;
    public bool IsInPlayerRange => isInPlayerRange;
    [HideInInspector] public bool isShadowFrozen;

    // Cached Non-Transparent Pixel Geometry (in SpriteRenderer local space)
    private Sprite cachedSprite;
    private float cachedAlphaThreshold = -1f;
    private int cachedSampleResolution = -1;
    private Vector2[] baseOpaqueVertices;
    private int[] baseOpaqueTriangles;
    private int[] flippedOpaqueTriangles;
    private List<Vector2[]> baseOpaqueContours = new List<Vector2[]>();
    private Vector2 opaqueCenter;
    private Vector3[] runtimeMeshVertices;
    private Vector2[] worldScaledBuffer;
    private bool geometryDirty = true;
    private float lastOrientationSign = 0f;

    void Awake()
    {
        spriteRenderer = GetComponentInParent<SpriteRenderer>();

        if (!TryGetComponent<MeshFilter>(out meshFilter))
            meshFilter = gameObject.AddComponent<MeshFilter>();

        if (!TryGetComponent<MeshRenderer>(out meshRenderer))
            meshRenderer = gameObject.AddComponent<MeshRenderer>();

        currentMesh = new Mesh();
        currentMesh.MarkDynamic();
        meshFilter.mesh = currentMesh;

        // Set initial preview material based on type
        UpdateMaterial();

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            playerTransform = playerObj.transform;
        }

        float objectScale = Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y));
        currentShadowDir = baseLightDirection.sqrMagnitude > 0.0001f ? baseLightDirection.normalized : Vector2.down;
        currentLength = minShadowLength * objectScale;

        EnsureOpaqueGeometryCached();
    }

    void Update()
    {
        UpdateSmoothedShadowValues();
        UpdateRealTimePreview();
    }

    void UpdateMaterial()
    {
        if (meshRenderer == null) return;

        if (currentShadowType == ShadowType.BlackSolid && blackShadowMaterial != null)
            meshRenderer.material = blackShadowMaterial;
        else if (currentShadowType == ShadowType.WhiteEthereal && whiteShadowMaterial != null)
            meshRenderer.material = whiteShadowMaterial;
    }

    void UpdateSmoothedShadowValues()
    {
        if (isShadowFrozen) return;

        float objectScale = Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y));
        Vector2 targetDirection = baseLightDirection.sqrMagnitude > 0.0001f ? baseLightDirection.normalized : currentShadowDir;
        float targetLength = minShadowLength * objectScale;

        if (affectedByPlayerPosition && playerTransform != null)
        {
            Vector2 casterToPlayer = (Vector2)playerTransform.position - (Vector2)transform.position;
            float distanceToPlayer = casterToPlayer.magnitude;

            if (distanceToPlayer <= maxInteractionDistance && distanceToPlayer > 0.0001f)
            {
                isInPlayerRange = true;
                Vector2 baseDir = baseLightDirection.sqrMagnitude > 0.0001f ? baseLightDirection.normalized : Vector2.zero;
                Vector2 playerDir = casterToPlayer.normalized * (shadowDirectionPull ? 1 : -1);

                Vector2 combinedDir = baseDir + (playerDir * playerRepulsionStrength);
                if (combinedDir.sqrMagnitude > 0.0001f)
                    targetDirection = combinedDir.normalized;

                float distanceFactor = Mathf.Clamp01(1f - (distanceToPlayer / maxInteractionDistance));
                targetLength = Mathf.Lerp(minShadowLength * objectScale, maxShadowLength * objectScale, distanceFactor);
            }
            else
            {
                isInPlayerRange = false;
            }
        }

        currentShadowDir = Vector2.Lerp(currentShadowDir, targetDirection, Time.deltaTime * transitionSpeed);
        currentLength = Mathf.Lerp(currentLength, targetLength, Time.deltaTime * transitionSpeed);
    }

    void UpdateRealTimePreview()
    {
        EnsureOpaqueGeometryCached();

        if (baseOpaqueVertices == null || baseOpaqueVertices.Length < 3 || baseOpaqueTriangles == null || baseOpaqueTriangles.Length < 3)
        {
            meshRenderer.enabled = false;
            return;
        }

        meshRenderer.enabled = true;

        int vertCount = baseOpaqueVertices.Length;
        if (runtimeMeshVertices == null || runtimeMeshVertices.Length != vertCount)
        {
            runtimeMeshVertices = new Vector3[vertCount];
        }

        TransformProjectedPoints(baseOpaqueVertices, runtimeMeshVertices, transform);

        float currentOrientationSign = GetOrientationSign(transform);
        bool topologyChanged = geometryDirty || currentMesh.vertexCount != vertCount || currentOrientationSign != lastOrientationSign;
        if (topologyChanged)
        {
            currentMesh.Clear();
            currentMesh.vertices = runtimeMeshVertices;
            currentMesh.triangles = GetOrientedTriangles(transform);
            geometryDirty = false;
            lastOrientationSign = currentOrientationSign;
        }
        else
        {
            currentMesh.vertices = runtimeMeshVertices;
        }

        currentMesh.RecalculateNormals();
        currentMesh.RecalculateBounds();
    }

    /// <summary>
    /// Returns projected world-space vertices of the outer non-transparent contour.
    /// </summary>
    public Vector2[] GetProjectedShadowVertices()
    {
        EnsureOpaqueGeometryCached();
        if (baseOpaqueVertices == null || baseOpaqueVertices.Length == 0) return null;

        Vector2[] sourcePoints = baseOpaqueContours.Count > 0 ? baseOpaqueContours[0] : baseOpaqueVertices;
        Vector2[] worldPoints = new Vector2[sourcePoints.Length];
        TransformProjectedWorldPoints(sourcePoints, worldPoints);
        return worldPoints;
    }

    public void SolidifyShadow()
    {
        EnsureOpaqueGeometryCached();
        if (baseOpaqueVertices == null || baseOpaqueVertices.Length < 3 || baseOpaqueTriangles == null || baseOpaqueTriangles.Length < 3)
            return;

        GameObject solidPlatform = new GameObject($"SolidifiedShadow_{currentShadowType}");
        solidPlatform.transform.position = transform.position;
        solidPlatform.transform.rotation = transform.rotation;

        MeshFilter mf = solidPlatform.AddComponent<MeshFilter>();
        MeshRenderer mr = solidPlatform.AddComponent<MeshRenderer>();
        PolygonCollider2D polyCollider = solidPlatform.AddComponent<PolygonCollider2D>();

        if (currentShadowType == ShadowType.BlackSolid)
        {
            solidPlatform.tag = "Ground";
            int groundLayer = LayerMask.NameToLayer("Ground");
            if (groundLayer != -1) solidPlatform.layer = groundLayer;
            polyCollider.isTrigger = false;
            if (blackShadowMaterial != null) mr.material = blackShadowMaterial;
        }
        else if (currentShadowType == ShadowType.WhiteEthereal)
        {
            solidPlatform.tag = "EtherealZone";
            polyCollider.isTrigger = true;
            if (whiteShadowMaterial != null) mr.material = whiteShadowMaterial;
        }

        Vector3[] localVertices = new Vector3[baseOpaqueVertices.Length];
        TransformProjectedPoints(baseOpaqueVertices, localVertices, solidPlatform.transform);

        Mesh solidMesh = new Mesh();
        solidMesh.vertices = localVertices;
        solidMesh.triangles = GetOrientedTriangles(solidPlatform.transform);
        solidMesh.RecalculateNormals();
        solidMesh.RecalculateBounds();
        mf.mesh = solidMesh;

        // Apply only non-transparent pixel contours (including holes and separate islands) to PolygonCollider2D
        polyCollider.pathCount = baseOpaqueContours.Count;
        for (int c = 0; c < baseOpaqueContours.Count; c++)
        {
            Vector2[] contour = baseOpaqueContours[c];
            Vector3[] projectedLocal3D = new Vector3[contour.Length];
            TransformProjectedPoints(contour, projectedLocal3D, solidPlatform.transform);

            Vector2[] colliderPath = new Vector2[contour.Length];
            for (int i = 0; i < contour.Length; i++)
            {
                colliderPath[i] = new Vector2(projectedLocal3D[i].x, projectedLocal3D[i].y);
            }
            polyCollider.SetPath(c, colliderPath);
        }

        Debug.Log($"[ShadowBound] Successfully baked {currentShadowType} shadow platform from non-transparent pixels!");
    }

    /// <summary>
    /// Projects sprite-local non-transparent points by shadowWidthFactor and currentShadowDir * currentLength
    /// into the target transform's local space.
    /// </summary>
    private void TransformProjectedPoints(Vector2[] spriteLocalPoints, Vector3[] outTargetLocalPoints, Transform targetTransform)
    {
        Transform spriteTransform = spriteRenderer != null ? spriteRenderer.transform : transform;
        bool flipX = spriteRenderer != null && spriteRenderer.flipX;
        bool flipY = spriteRenderer != null && spriteRenderer.flipY;

        Vector2 worldDir = currentShadowDir * currentLength;
        Vector2 shadowAxis = currentShadowDir.sqrMagnitude > 0.0001f ? currentShadowDir.normalized : Vector2.down;

        int count = spriteLocalPoints.Length;
        if (worldScaledBuffer == null || worldScaledBuffer.Length < count)
        {
            worldScaledBuffer = new Vector2[count];
        }

        float minProj = float.MaxValue;
        float maxProj = float.MinValue;

        for (int i = 0; i < count; i++)
        {
            Vector2 pt = spriteLocalPoints[i];
            Vector2 scaled = opaqueCenter + (pt - opaqueCenter) * shadowWidthFactor;
            if (flipX) scaled.x = -scaled.x;
            if (flipY) scaled.y = -scaled.y;

            Vector2 worldPt = spriteTransform.TransformPoint(scaled);
            worldScaledBuffer[i] = worldPt;

            float proj = Vector2.Dot(worldPt, shadowAxis);
            if (proj < minProj) minProj = proj;
            if (proj > maxProj) maxProj = proj;
        }

        float projSpan = Mathf.Max(maxProj - minProj, 0.0001f);

        for (int i = 0; i < count; i++)
        {
            Vector2 worldPt = worldScaledBuffer[i];
            float t = Mathf.Clamp01((Vector2.Dot(worldPt, shadowAxis) - minProj) / projSpan);
            Vector2 projectedWorld = worldPt + worldDir * t;

            Vector3 localPos = targetTransform.InverseTransformPoint(projectedWorld);
            outTargetLocalPoints[i] = new Vector3(localPos.x, localPos.y, 0f);
        }
    }

    private void TransformProjectedWorldPoints(Vector2[] spriteLocalPoints, Vector2[] outWorldPoints)
    {
        Transform spriteTransform = spriteRenderer != null ? spriteRenderer.transform : transform;
        bool flipX = spriteRenderer != null && spriteRenderer.flipX;
        bool flipY = spriteRenderer != null && spriteRenderer.flipY;

        Vector2 worldDir = currentShadowDir * currentLength;
        Vector2 shadowAxis = currentShadowDir.sqrMagnitude > 0.0001f ? currentShadowDir.normalized : Vector2.down;

        int count = spriteLocalPoints.Length;
        float minProj = float.MaxValue;
        float maxProj = float.MinValue;

        for (int i = 0; i < count; i++)
        {
            Vector2 pt = spriteLocalPoints[i];
            Vector2 scaled = opaqueCenter + (pt - opaqueCenter) * shadowWidthFactor;
            if (flipX) scaled.x = -scaled.x;
            if (flipY) scaled.y = -scaled.y;

            Vector2 worldPt = spriteTransform.TransformPoint(scaled);
            outWorldPoints[i] = worldPt;

            float proj = Vector2.Dot(worldPt, shadowAxis);
            if (proj < minProj) minProj = proj;
            if (proj > maxProj) maxProj = proj;
        }

        float projSpan = Mathf.Max(maxProj - minProj, 0.0001f);

        for (int i = 0; i < count; i++)
        {
            Vector2 worldPt = outWorldPoints[i];
            float t = Mathf.Clamp01((Vector2.Dot(worldPt, shadowAxis) - minProj) / projSpan);
            outWorldPoints[i] = worldPt + worldDir * t;
        }
    }

    private float GetOrientationSign(Transform targetTransform)
    {
        Transform spriteTransform = spriteRenderer != null ? spriteRenderer.transform : transform;
        bool flipX = spriteRenderer != null && spriteRenderer.flipX;
        bool flipY = spriteRenderer != null && spriteRenderer.flipY;

        float spriteSign = Mathf.Sign(spriteTransform.lossyScale.x * spriteTransform.lossyScale.y);
        if (flipX) spriteSign = -spriteSign;
        if (flipY) spriteSign = -spriteSign;

        float targetSign = Mathf.Sign(targetTransform.lossyScale.x * targetTransform.lossyScale.y);
        return spriteSign * targetSign;
    }

    private int[] GetOrientedTriangles(Transform targetTransform)
    {
        if (GetOrientationSign(targetTransform) >= 0f)
            return baseOpaqueTriangles;

        if (flippedOpaqueTriangles == null || flippedOpaqueTriangles.Length != baseOpaqueTriangles.Length)
        {
            flippedOpaqueTriangles = new int[baseOpaqueTriangles.Length];
            for (int i = 0; i < baseOpaqueTriangles.Length; i += 3)
            {
                flippedOpaqueTriangles[i] = baseOpaqueTriangles[i];
                flippedOpaqueTriangles[i + 1] = baseOpaqueTriangles[i + 2];
                flippedOpaqueTriangles[i + 2] = baseOpaqueTriangles[i + 1];
            }
        }
        return flippedOpaqueTriangles;
    }

    /// <summary>
    /// Reads the sprite texture's alpha channel and builds a mesh + collider contours
    /// strictly from non-transparent pixels (alpha > alphaThreshold).
    /// </summary>
    private void EnsureOpaqueGeometryCached()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInParent<SpriteRenderer>();

        if (spriteRenderer == null || spriteRenderer.sprite == null)
        {
            baseOpaqueVertices = null;
            baseOpaqueTriangles = null;
            flippedOpaqueTriangles = null;
            baseOpaqueContours.Clear();
            return;
        }

        Sprite sprite = spriteRenderer.sprite;
        int clampedRes = Mathf.Clamp(maxPixelSampleResolution, 16, 256);

        if (cachedSprite == sprite &&
            Mathf.Approximately(cachedAlphaThreshold, alphaThreshold) &&
            cachedSampleResolution == clampedRes &&
            baseOpaqueVertices != null)
        {
            return;
        }

        cachedSprite = sprite;
        cachedAlphaThreshold = alphaThreshold;
        cachedSampleResolution = clampedRes;
        flippedOpaqueTriangles = null;
        geometryDirty = true;

        BuildOpaquePixelGeometry(sprite, alphaThreshold, clampedRes);
    }

    private void BuildOpaquePixelGeometry(Sprite sprite, float threshold, int maxRes)
    {
        baseOpaqueContours.Clear();

        Rect texRect = sprite.textureRect;
        int rectX = Mathf.RoundToInt(texRect.x);
        int rectY = Mathf.RoundToInt(texRect.y);
        int rectW = Mathf.RoundToInt(texRect.width);
        int rectH = Mathf.RoundToInt(texRect.height);

        if (rectW <= 0 || rectH <= 0 || sprite.texture == null)
        {
            baseOpaqueVertices = null;
            baseOpaqueTriangles = null;
            return;
        }

        Color32[] pixels = ReadSpritePixels(sprite.texture, rectX, rectY, rectW, rectH);
        if (pixels == null || pixels.Length != rectW * rectH)
        {
            baseOpaqueVertices = null;
            baseOpaqueTriangles = null;
            return;
        }

        // Determine sampling grid dimensions (1:1 for small sprites, downsampled for large HD textures)
        float scale = Mathf.Min(1f, (float)maxRes / Mathf.Max(rectW, rectH));
        int gridW = Mathf.Max(1, Mathf.RoundToInt(rectW * scale));
        int gridH = Mathf.Max(1, Mathf.RoundToInt(rectH * scale));

        bool[,] opaqueGrid = new bool[gridW, gridH];
        byte alphaCutoff = (byte)Mathf.Clamp(Mathf.RoundToInt(threshold * 255f), 1, 254);

        Vector2 pivot = sprite.pivot;
        float ppu = sprite.pixelsPerUnit;
        float cellPixelW = (float)rectW / gridW;
        float cellPixelH = (float)rectH / gridH;

        Vector2 centerAccum = Vector2.zero;
        int opaqueCellCount = 0;

        for (int gy = 0; gy < gridH; gy++)
        {
            int pyStart = Mathf.FloorToInt(gy * cellPixelH);
            int pyEnd = Mathf.Min(rectH, Mathf.CeilToInt((gy + 1) * cellPixelH));

            for (int gx = 0; gx < gridW; gx++)
            {
                int pxStart = Mathf.FloorToInt(gx * cellPixelW);
                int pxEnd = Mathf.Min(rectW, Mathf.CeilToInt((gx + 1) * cellPixelW));

                bool isCellOpaque = false;
                for (int py = pyStart; py < pyEnd && !isCellOpaque; py++)
                {
                    int rowOffset = py * rectW;
                    for (int px = pxStart; px < pxEnd; px++)
                    {
                        if (pixels[rowOffset + px].a > alphaCutoff)
                        {
                            isCellOpaque = true;
                            break;
                        }
                    }
                }

                opaqueGrid[gx, gy] = isCellOpaque;
                if (isCellOpaque)
                {
                    float localX = ((gx + 0.5f) * cellPixelW - pivot.x) / ppu;
                    float localY = ((gy + 0.5f) * cellPixelH - pivot.y) / ppu;
                    centerAccum += new Vector2(localX, localY);
                    opaqueCellCount++;
                }
            }
        }

        if (opaqueCellCount == 0)
        {
            baseOpaqueVertices = null;
            baseOpaqueTriangles = null;
            return;
        }

        opaqueCenter = centerAccum / opaqueCellCount;

        // 1. Greedy Meshing of Non-Transparent Cells for the Shadow Mesh
        bool[,] visited = new bool[gridW, gridH];
        List<Vector2> verts = new List<Vector2>();
        List<int> tris = new List<int>();
        Dictionary<long, int> vertexIndexMap = new Dictionary<long, int>();

        int GetOrAddGridVertex(int vx, int vy)
        {
            long key = ((long)vx << 32) | (uint)vy;
            if (vertexIndexMap.TryGetValue(key, out int existingIndex))
                return existingIndex;

            float localX = (vx * cellPixelW - pivot.x) / ppu;
            float localY = (vy * cellPixelH - pivot.y) / ppu;
            int newIndex = verts.Count;
            verts.Add(new Vector2(localX, localY));
            vertexIndexMap[key] = newIndex;
            return newIndex;
        }

        for (int gy = 0; gy < gridH; gy++)
        {
            for (int gx = 0; gx < gridW; gx++)
            {
                if (!opaqueGrid[gx, gy] || visited[gx, gy])
                    continue;

                // Expand width along X
                int quadW = 1;
                while (gx + quadW < gridW && opaqueGrid[gx + quadW, gy] && !visited[gx + quadW, gy])
                {
                    quadW++;
                }

                // Expand height along Y while the full row span is opaque and unvisited
                int quadH = 1;
                while (gy + quadH < gridH)
                {
                    bool rowValid = true;
                    for (int k = 0; k < quadW; k++)
                    {
                        if (!opaqueGrid[gx + k, gy + quadH] || visited[gx + k, gy + quadH])
                        {
                            rowValid = false;
                            break;
                        }
                    }
                    if (!rowValid) break;
                    quadH++;
                }

                // Mark cells as visited
                for (int dy = 0; dy < quadH; dy++)
                {
                    for (int dx = 0; dx < quadW; dx++)
                    {
                        visited[gx + dx, gy + dy] = true;
                    }
                }

                // Emit quad (counter-clockwise in 2D -> clockwise for Unity -Z camera normal)
                int v0 = GetOrAddGridVertex(gx, gy);
                int v1 = GetOrAddGridVertex(gx, gy + quadH);
                int v2 = GetOrAddGridVertex(gx + quadW, gy + quadH);
                int v3 = GetOrAddGridVertex(gx + quadW, gy);

                tris.Add(v0);
                tris.Add(v1);
                tris.Add(v2);

                tris.Add(v0);
                tris.Add(v2);
                tris.Add(v3);
            }
        }

        baseOpaqueVertices = verts.ToArray();
        baseOpaqueTriangles = tris.ToArray();

        // 2. Trace Closed Boundary Contours (Outer Islands + Inner Holes) for PolygonCollider2D
        TraceOpaqueContours(opaqueGrid, gridW, gridH, cellPixelW, cellPixelH, pivot, ppu);
    }

    /// <summary>
    /// Safely reads pixels from a sprite's textureRect even when TextureImporter 'Read/Write Enabled' is false.
    /// </summary>
    private static Color32[] ReadSpritePixels(Texture2D sourceTexture, int x, int y, int width, int height)
    {
        if (sourceTexture.isReadable)
        {
            Color[] colors = sourceTexture.GetPixels(x, y, width, height);
            Color32[] result = new Color32[colors.Length];
            for (int i = 0; i < colors.Length; i++)
                result[i] = colors[i];
            return result;
        }

        RenderTexture rt = RenderTexture.GetTemporary(
            sourceTexture.width,
            sourceTexture.height,
            0,
            RenderTextureFormat.Default,
            RenderTextureReadWrite.Linear);

        Graphics.Blit(sourceTexture, rt);
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = rt;

        Texture2D tempTex = new Texture2D(sourceTexture.width, sourceTexture.height, TextureFormat.RGBA32, false);
        tempTex.ReadPixels(new Rect(0, 0, sourceTexture.width, sourceTexture.height), 0, 0);
        tempTex.Apply();

        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(rt);

        Color[] subColors = tempTex.GetPixels(x, y, width, height);
        Destroy(tempTex);

        Color32[] subResult = new Color32[subColors.Length];
        for (int i = 0; i < subColors.Length; i++)
            subResult[i] = subColors[i];

        return subResult;
    }

    private struct GridEdge : IEquatable<GridEdge>
    {
        public Vector2Int start;
        public Vector2Int end;

        public GridEdge(Vector2Int s, Vector2Int e)
        {
            start = s;
            end = e;
        }

        public bool Equals(GridEdge other) => start == other.start && end == other.end;
        public override bool Equals(object obj) => obj is GridEdge other && Equals(other);
        public override int GetHashCode() => (start.GetHashCode() * 397) ^ end.GetHashCode();
    }

    /// <summary>
    /// Extracts simplified polygon loops around all non-transparent pixel boundaries (including interior holes).
    /// </summary>
    private void TraceOpaqueContours(bool[,] opaqueGrid, int gridW, int gridH, float cellPixelW, float cellPixelH, Vector2 pivot, float ppu)
    {
        Dictionary<Vector2Int, List<Vector2Int>> outgoingByVertex = new Dictionary<Vector2Int, List<Vector2Int>>();
        List<GridEdge> allEdges = new List<GridEdge>();

        void AddDirectedEdge(Vector2Int a, Vector2Int b)
        {
            if (!outgoingByVertex.TryGetValue(a, out List<Vector2Int> list))
            {
                list = new List<Vector2Int>(2);
                outgoingByVertex[a] = list;
            }
            list.Add(b);
            allEdges.Add(new GridEdge(a, b));
        }

        bool IsOpaque(int x, int y) => x >= 0 && x < gridW && y >= 0 && y < gridH && opaqueGrid[x, y];

        for (int y = 0; y < gridH; y++)
        {
            for (int x = 0; x < gridW; x++)
            {
                if (!opaqueGrid[x, y]) continue;

                // Bottom edge
                if (!IsOpaque(x, y - 1))
                    AddDirectedEdge(new Vector2Int(x, y), new Vector2Int(x + 1, y));
                // Right edge
                if (!IsOpaque(x + 1, y))
                    AddDirectedEdge(new Vector2Int(x + 1, y), new Vector2Int(x + 1, y + 1));
                // Top edge
                if (!IsOpaque(x, y + 1))
                    AddDirectedEdge(new Vector2Int(x + 1, y + 1), new Vector2Int(x, y + 1));
                // Left edge
                if (!IsOpaque(x - 1, y))
                    AddDirectedEdge(new Vector2Int(x, y + 1), new Vector2Int(x, y));
            }
        }

        HashSet<GridEdge> usedEdges = new HashSet<GridEdge>();

        for (int i = 0; i < allEdges.Count; i++)
        {
            GridEdge startEdge = allEdges[i];
            if (usedEdges.Contains(startEdge)) continue;

            List<Vector2Int> loopGridPts = new List<Vector2Int>();
            GridEdge currentEdge = startEdge;

            while (true)
            {
                usedEdges.Add(currentEdge);
                loopGridPts.Add(currentEdge.start);

                Vector2Int currentVertex = currentEdge.end;
                if (currentVertex == startEdge.start)
                    break;

                if (!outgoingByVertex.TryGetValue(currentVertex, out List<Vector2Int> candidates))
                    break;

                Vector2Int prevDir = currentEdge.end - currentEdge.start;
                Vector2Int chosenNext = candidates[0];
                bool foundUnused = false;

                // Prefer right-turn / straight continuity when two contours touch at a corner
                int bestPriority = int.MinValue;
                for (int c = 0; c < candidates.Count; c++)
                {
                    Vector2Int nextV = candidates[c];
                    GridEdge candidateEdge = new GridEdge(currentVertex, nextV);
                    if (usedEdges.Contains(candidateEdge)) continue;

                    Vector2Int nextDir = nextV - currentVertex;
                    int cross = prevDir.x * nextDir.y - prevDir.y * nextDir.x;
                    int dot = prevDir.x * nextDir.x + prevDir.y * nextDir.y;
                    int priority = cross < 0 ? 3 : (dot > 0 ? 2 : (cross > 0 ? 1 : 0));

                    if (!foundUnused || priority > bestPriority)
                    {
                        foundUnused = true;
                        bestPriority = priority;
                        chosenNext = nextV;
                    }
                }

                if (!foundUnused) break;
                currentEdge = new GridEdge(currentVertex, chosenNext);
            }

            if (loopGridPts.Count < 3) continue;

            // Remove collinear intermediate grid vertices so straight edges have only 2 endpoints
            List<Vector2> simplified = new List<Vector2>();
            int count = loopGridPts.Count;
            for (int k = 0; k < count; k++)
            {
                Vector2Int prev = loopGridPts[(k - 1 + count) % count];
                Vector2Int curr = loopGridPts[k];
                Vector2Int next = loopGridPts[(k + 1) % count];

                Vector2Int d1 = curr - prev;
                Vector2Int d2 = next - curr;
                if (d1.x * d2.y - d1.y * d2.x != 0)
                {
                    float localX = (curr.x * cellPixelW - pivot.x) / ppu;
                    float localY = (curr.y * cellPixelH - pivot.y) / ppu;
                    simplified.Add(new Vector2(localX, localY));
                }
            }

            if (simplified.Count >= 3)
            {
                baseOpaqueContours.Add(simplified.ToArray());
            }
        }
    }
}
