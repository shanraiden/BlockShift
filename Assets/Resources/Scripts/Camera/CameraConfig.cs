using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraConfig : MonoBehaviour
{
    [Header("Scaling Settings")]
    public float sceneWidth = 10f;

    [Header("Follow Settings")]
    public Transform worldPosFollow;          // Drag your player here
    public float smoothSpeed = 0.125f; // Lower = smoother
    public Vector3 offset = new Vector3(0, 0, -10);

    [Header("Boundary Settings")]
    public bool useBounds = true;
    public RectTransform boundaryRectTransform; // Drag your UI/World RectTransform boundary here

    private Vector2 minWorldBounds;
    private Vector2 maxWorldBounds;

    
    public bool isCameraFollowingPlayer = true;

    // Cached References
    private Camera targetCamera;

    private void Awake()
    {
        targetCamera = GetComponent<Camera>();
        
        FetchWorldBoundsFromRect();
    }


    public void FetchWorldBoundsFromRect()
    {
        if (!useBounds) return;

        if (boundaryRectTransform == null)
        {
            GameObject boundObj = GameObject.Find("CameraBound");
            if (boundObj != null)
            {
                boundObj.TryGetComponent<RectTransform>(out boundaryRectTransform);
            }
        }

        if (boundaryRectTransform != null)
        {
            Vector3[] corners = new Vector3[4];
            boundaryRectTransform.GetWorldCorners(corners);

            // corners[0] = Bottom-Left, corners[2] = Top-Right
            minWorldBounds = corners[0];
            maxWorldBounds = corners[2];
        }
        else
        {
            Debug.LogWarning("[CameraConfig] Boundary RectTransform is missing!");
        }
    }

    private void FixedUpdate()
    {
        // 1. Calculate & Set Orthographic Size
        float unitsPerPixel = sceneWidth / Screen.width;
        float halfHeight = 0.5f * unitsPerPixel * Screen.height;
        targetCamera.orthographicSize = halfHeight;

        // Calculate half-width of the camera viewport in world units
        float halfWidth = halfHeight * targetCamera.aspect;
        // 2. Smooth Follow Logic
        if ( isCameraFollowingPlayer)
        {
            FollowTarget(worldPosFollow.position, halfWidth, halfHeight);
        }
    }

    private void FollowTarget(Vector3 targetWorldPosition, float halfWidth, float halfHeight)
    {
        // 1. Calculate the direction vector from the camera's current position to the target
        Vector3 directionToTarget = targetWorldPosition - transform.position;

        // Ignore Z when calculating direction so depth remains constant
        directionToTarget.z = 0f;

        // 2. Determine target position using the movement direction
        Vector3 desiredPosition;

        if (directionToTarget.sqrMagnitude > 0.001f)
        {
            // Move in the normalized direction of the target + offset
            Vector3 moveDirection = directionToTarget.normalized;
            desiredPosition = transform.position + (moveDirection * directionToTarget.magnitude) + offset;
        }
        else
        {
            desiredPosition = targetWorldPosition + offset;
        }

        // 3. Constrain within boundary limits
        if (useBounds)
        {
            float minX = minWorldBounds.x + halfWidth;
            float maxX = maxWorldBounds.x - halfWidth;
            float minY = minWorldBounds.y + halfHeight;
            float maxY = maxWorldBounds.y - halfHeight;

            desiredPosition.x = (minX > maxX)
                ? (minWorldBounds.x + maxWorldBounds.x) * 0.5f
                : Mathf.Clamp(desiredPosition.x, minX, maxX);

            desiredPosition.y = (minY > maxY)
                ? (minWorldBounds.y + maxWorldBounds.y) * 0.5f
                : Mathf.Clamp(desiredPosition.y, minY, maxY);
        }

        // Maintain depth
        desiredPosition.z = transform.position.z;

        // 4. Frame-rate independent smooth interpolation
        transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
    }
}