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
    internal bool isShadowFrozen;
    void Awake()
    {
        spriteRenderer = GetComponentInParent<SpriteRenderer>();

        if (!TryGetComponent<MeshFilter>(out meshFilter))
            meshFilter = gameObject.AddComponent<MeshFilter>();

        if (!TryGetComponent<MeshRenderer>(out meshRenderer))
            meshRenderer = gameObject.AddComponent<MeshRenderer>();

        currentMesh = new Mesh();
        meshFilter.mesh = currentMesh;

        // Set initial preview material based on type
        UpdateMaterial();

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            playerTransform = playerObj.transform;
        }

        float objectScale = Mathf.Max(transform.lossyScale.x, transform.lossyScale.y);
        currentShadowDir = baseLightDirection.normalized;
        currentLength = minShadowLength * objectScale;
    }

    void Update()
    {
        UpdateSmoothedShadowValues();
        UpdateRealTimePreview();
        print(isInPlayerRange);
        
        
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
        float objectScale = Mathf.Max(transform.lossyScale.x, transform.lossyScale.y);
        Vector2 targetDirection = baseLightDirection.normalized;
        float targetLength = minShadowLength * objectScale;

        if (affectedByPlayerPosition && playerTransform != null)
        {
            Vector2 casterToPlayer = (Vector2)playerTransform.position - (Vector2)transform.position;
            float distanceToPlayer = casterToPlayer.magnitude;

            if (distanceToPlayer <= maxInteractionDistance && distanceToPlayer > 0.0001f && !isShadowFrozen)
            {
                isInPlayerRange = true;
                Vector2 baseDir = baseLightDirection.normalized;
                Vector2 playerDir = casterToPlayer.normalized * (shadowDirectionPull ? 1 : -1);

                targetDirection = (baseDir + (playerDir * playerRepulsionStrength)).normalized;

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
        Vector2[] shadowPoints = GetProjectedShadowVertices();
        if (shadowPoints == null || shadowPoints.Length < 3)
        {
            meshRenderer.enabled = false;
            return;
        }

        meshRenderer.enabled = true;
        Triangulator triangulator = new Triangulator(shadowPoints);
        int[] indices = triangulator.Triangulate();

        currentMesh.Clear();
        Vector3[] vertices = new Vector3[shadowPoints.Length];

        for (int i = 0; i < shadowPoints.Length; i++)
        {
            Vector3 localPos = transform.InverseTransformPoint(shadowPoints[i]);
            vertices[i] = new Vector3(localPos.x, localPos.y, 0);
        }

        currentMesh.vertices = vertices;
        currentMesh.triangles = indices;
        currentMesh.RecalculateNormals();
        currentMesh.RecalculateBounds();
    }

    public Vector2[] GetProjectedShadowVertices()
    {
        if (spriteRenderer == null || spriteRenderer.sprite == null) return null;

        Vector2[] spriteVertices = spriteRenderer.sprite.vertices;
        Vector2[] shadowPath = new Vector2[spriteVertices.Length * 2];

        Vector2 vertexCenter = Vector2.zero;
        for (int i = 0; i < spriteVertices.Length; i++)
        {
            vertexCenter += spriteVertices[i];
        }
        vertexCenter /= spriteVertices.Length;

        Vector2 dir = currentShadowDir * currentLength;

        for (int i = 0; i < spriteVertices.Length; i++)
        {
            Vector2 scaledVertex = vertexCenter + (spriteVertices[i] - vertexCenter) * shadowWidthFactor;
            Vector2 worldPoint = transform.TransformPoint(scaledVertex);
            shadowPath[i] = worldPoint;
            shadowPath[spriteVertices.Length + i] = worldPoint + dir;
        }

        return ConvexHull2D.ComputeHull(shadowPath);
    }

    public void SolidifyShadow()
    {
        Vector2[] worldHullPoints = GetProjectedShadowVertices();
        if (worldHullPoints == null || worldHullPoints.Length < 3) return;

        GameObject solidPlatform = new GameObject($"SolidifiedShadow_{currentShadowType}");
        solidPlatform.transform.position = transform.position;
        solidPlatform.transform.rotation = transform.rotation;

        MeshFilter mf = solidPlatform.AddComponent<MeshFilter>();
        MeshRenderer mr = solidPlatform.AddComponent<MeshRenderer>();
        PolygonCollider2D polyCollider = solidPlatform.AddComponent<PolygonCollider2D>();

        if (currentShadowType == ShadowType.BlackSolid)
        {
            solidPlatform.tag = "Ground";
            polyCollider.isTrigger = false;
            if (blackShadowMaterial != null) mr.material = blackShadowMaterial;
        }
        else if (currentShadowType == ShadowType.WhiteEthereal)
        {
            solidPlatform.tag = "EtherealZone";
            polyCollider.isTrigger = true;
            if (whiteShadowMaterial != null) mr.material = whiteShadowMaterial;
        }

        Mesh solidMesh = new Mesh();
        Triangulator triangulator = new Triangulator(worldHullPoints);
        int[] indices = triangulator.Triangulate();

        Vector3[] localVertices = new Vector3[worldHullPoints.Length];
        Vector2[] localColliderPoints = new Vector2[worldHullPoints.Length];

        for (int i = 0; i < worldHullPoints.Length; i++)
        {
            Vector3 localPos = solidPlatform.transform.InverseTransformPoint(worldHullPoints[i]);
            localVertices[i] = new Vector3(localPos.x, localPos.y, 0);
            localColliderPoints[i] = new Vector2(localPos.x, localPos.y);
        }

        solidMesh.vertices = localVertices;
        solidMesh.triangles = indices;
        solidMesh.RecalculateNormals();
        solidMesh.RecalculateBounds();
        mf.mesh = solidMesh;

        polyCollider.SetPath(0, localColliderPoints);
        Debug.Log($"[ShadowBound] Successfully baked {currentShadowType} shadow platform!");
    }
}

public class Triangulator
{
    private List<Vector2> m_points = new List<Vector2>();

    public Triangulator(Vector2[] points)
    {
        m_points = new List<Vector2>(points);
    }

    public int[] Triangulate()
    {
        List<int> indices = new List<int>();
        int n = m_points.Count;
        if (n < 3) return indices.ToArray();

        int[] V = new int[n];
        if (Area() > 0)
            for (int v = 0; v < n; v++) V[v] = v;
        else
            for (int v = 0; v < n; v++) V[v] = (n - 1) - v;

        int nv = n;
        int count = 2 * nv;
        for (int m = 0, v = nv - 1; nv > 2;)
        {
            if ((count--) <= 0) return indices.ToArray();

            int u = v; if (nv <= u) u = 0;
            v = u + 1; if (nv <= v) v = 0;
            int w = v + 1; if (nv <= w) w = 0;

            if (Snip(u, v, w, nv, V))
            {
                int a = V[u], b = V[v], c = V[w];
                indices.Add(a); indices.Add(b); indices.Add(c);
                m++;
                for (int s = v, t = v + 1; t < nv; s++, t++) V[s] = V[t];
                nv--;
                count = 2 * nv;
            }
        }
        indices.Reverse();
        return indices.ToArray();
    }

    private float Area()
    {
        int n = m_points.Count;
        float A = 0.0f;
        for (int p = n - 1, q = 0; q < n; p = q++)
            A += m_points[p].x * m_points[q].y - m_points[q].x * m_points[p].y;
        return A * 0.5f;
    }

    private bool Snip(int u, int v, int w, int n, int[] V)
    {
        Vector2 A = m_points[V[u]], B = m_points[V[v]], C = m_points[V[w]];
        if (Mathf.Epsilon > (((B.x - A.x) * (C.y - A.y)) - ((B.y - A.y) * (C.x - A.x)))) return false;
        for (int p = 0; p < n; p++)
        {
            if ((p == u) || (p == v) || (p == w)) continue;
            if (InsideTriangle(A, B, C, m_points[V[p]])) return false;
        }
        return true;
    }

    private bool InsideTriangle(Vector2 A, Vector2 B, Vector2 C, Vector2 P)
    {
        float ax = C.x - B.x, ay = C.y - B.y;
        float bx = A.x - C.x, by = A.y - C.y;
        float cx = B.x - A.x, cy = B.y - A.y;
        float apx = P.x - A.x, apy = P.y - A.y;
        float bpx = P.x - B.x, bpy = P.y - B.y;
        float cpx = P.x - C.x, cpy = P.y - C.y;

        return ((ax * bpy - ay * bpx >= 0.0f) && (cx * apy - cy * apx >= 0.0f) && (bx * cpy - by * cpx >= 0.0f));
    }
}

public static class ConvexHull2D
{
    public static Vector2[] ComputeHull(Vector2[] pts)
    {
        int n = pts.Length;
        if (n <= 3) return pts;

        Array.Sort(pts, (a, b) => a.x == b.x ? a.y.CompareTo(b.y) : a.x.CompareTo(b.x));

        List<Vector2> hull = new List<Vector2>();

        // Lower hull
        for (int i = 0; i < n; i++)
        {
            while (hull.Count >= 2 && Cross(hull[hull.Count - 2], hull[hull.Count - 1], pts[i]) <= 0)
                hull.RemoveAt(hull.Count - 1);
            hull.Add(pts[i]);
        }

        // Upper hull
        for (int i = n - 2, t = hull.Count + 1; i >= 0; i--)
        {
            while (hull.Count >= t && Cross(hull[hull.Count - 2], hull[hull.Count - 1], pts[i]) <= 0)
                hull.RemoveAt(hull.Count - 1);
            hull.Add(pts[i]);
        }

        hull.RemoveAt(hull.Count - 1);
        return hull.ToArray();
    }

    private static float Cross(Vector2 O, Vector2 A, Vector2 B)
    {
        return (A.x - O.x) * (B.y - O.y) - (A.y - O.y) * (B.x - O.x);
    }
}