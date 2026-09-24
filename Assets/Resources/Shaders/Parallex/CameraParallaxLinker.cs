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
        if (targetCamera == null && Camera.main != null)
        {
            targetCamera = Camera.main.transform;
        }

        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<Renderer>();
        }

        _propertyBlock = new MaterialPropertyBlock();
    }

    private void LateUpdate()
    {
        if (targetCamera == null || spriteRenderer == null) return;

        // Fetch camera world X coordinate
        float camX = targetCamera.position.x;

        // Efficient property block update (no material instance allocations)
        spriteRenderer.GetPropertyBlock(_propertyBlock);
        _propertyBlock.SetFloat(CamPosXID, camX);
        spriteRenderer.SetPropertyBlock(_propertyBlock);
    }
}