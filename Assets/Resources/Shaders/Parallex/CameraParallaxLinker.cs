using UnityEngine;

[ExecuteInEditMode]
public class CameraParallaxLinker : MonoBehaviour
{
    [SerializeField] private Transform targetCamera;
    [SerializeField] private Renderer spriteRenderer;

    private static readonly int CamPosXID = Shader.PropertyToID("_CamPosX");
    private MaterialPropertyBlock _propertyBlock;

    private void Awake()
    {
        Initialize();
    }

    private void OnEnable()
    {
        Initialize();
    }

    private void Initialize()
    {
        if (targetCamera == null && Camera.main != null)
        {
            targetCamera = Camera.main.transform;
        }

        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<Renderer>();
        }

        if (_propertyBlock == null)
        {
            _propertyBlock = new MaterialPropertyBlock();
        }
    }

    private void LateUpdate()
    {
        // Guard against destroyed components or unassigned cameras
        if (targetCamera == null || spriteRenderer == null) return;

        // Lazy initialization check for Edit Mode / runtime instantiation
        if (_propertyBlock == null)
        {
            _propertyBlock = new MaterialPropertyBlock();
        }

        // Fetch camera world X coordinate
        float camX = targetCamera.position.x;

        // Safely update material property block without allocations
        spriteRenderer.GetPropertyBlock(_propertyBlock);
        _propertyBlock.SetFloat(CamPosXID, camX);
        spriteRenderer.SetPropertyBlock(_propertyBlock);
    }
}