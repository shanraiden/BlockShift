using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Camera))]
public class CameraConfig : MonoBehaviour
{
    [Header("Scaling Settings")]
    public float sceneWidth = 10f;

    [Header("Follow Settings")]
    public Transform worldPosFollow;          // Drag your player here
    [Tooltip("Time in seconds for camera to reach player. Smaller value = faster/snappier, Larger value = smoother/slower.")]
    public float followSmoothTime = 0.2f; 
    public Vector3 offset = new Vector3(0, 0, -10);
    public bool isCameraFollowingPlayer = true;

    [Header("Pan / Drag Settings (New Input System)")]
    [Tooltip("Reference to an Input Action for screen contact/press (e.g. Pointer/press or Touchscreen/PrimaryTouch/press)")]
    [SerializeField] private InputActionProperty touchContactAction;

    [Tooltip("Reference to an Input Action for screen position (e.g. Pointer/position or Touchscreen/PrimaryTouch/position)")]
    [SerializeField] private InputActionProperty touchPositionAction;

    [Tooltip("Multiplier for camera panning speed.")]
    [SerializeField] private float panSensitivity = 1.0f;

    [Header("Inertia / Glide Settings")]
    [Tooltip("Enable smooth momentum glide after releasing a drag or swipe.")]
    public bool enableInertia = true;

    [Tooltip("Damping factor for glide momentum (lower = stops faster, higher = glides further). Range 0.80 - 0.99 recommended.")]
    [Range(0.80f, 0.99f)]
    public float decelerationRate = 0.92f;

    [Header("Boundary Settings")]
    public bool useBounds = true;
    public RectTransform boundaryRectTransform; // Drag your UI/World RectTransform boundary here

    private Vector2 minWorldBounds;
    private Vector2 maxWorldBounds;

    // Internal State
    private Camera targetCamera;
    private Vector3 targetPosition;
    private Vector2 lastScreenPosition;
    private Vector3 panVelocity;
    private Vector3 currentSmoothDampVelocity; // Used by SmoothDamp
    private bool isDragging = false;

    private void Awake()
    {
        targetCamera = GetComponent<Camera>();
        targetPosition = transform.position;

        FetchWorldBoundsFromRect();
    }

    private void OnEnable()
    {
        touchContactAction.action?.Enable();
        touchPositionAction.action?.Enable();

        if (touchContactAction.action != null)
        {
            touchContactAction.action.started += OnDragStart;
            touchContactAction.action.canceled += OnDragEnd;
        }
    }

    private void OnDisable()
    {
        if (touchContactAction.action != null)
        {
            touchContactAction.action.started -= OnDragStart;
            touchContactAction.action.canceled -= OnDragEnd;
        }

        touchContactAction.action?.Disable();
        touchPositionAction.action?.Disable();
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

    private void OnDragStart(InputAction.CallbackContext context)
    {
        isDragging = true;
        panVelocity = Vector3.zero;
        currentSmoothDampVelocity = Vector3.zero;
        lastScreenPosition = touchPositionAction.action.ReadValue<Vector2>();
        targetPosition = transform.position;
    }

    private void OnDragEnd(InputAction.CallbackContext context)
    {
        isDragging = false;
    }

    private void LateUpdate()
    {
        // 1. Calculate & Set Orthographic Size based on design width
        float unitsPerPixel = sceneWidth / Screen.width;
        float halfHeight = 0.5f * unitsPerPixel * Screen.height;
        targetCamera.orthographicSize = halfHeight;

        // Calculate half-width of camera viewport in world units
        float halfWidth = halfHeight * targetCamera.aspect;

        // 2. Process Movement Mode
        if (isDragging)
        {
            Vector2 currentScreenPos = touchPositionAction.action.ReadValue<Vector2>();
            Vector2 screenDelta = lastScreenPosition - currentScreenPos;

            // Convert screen pixel delta to world space displacement
            float worldPerPixel = (targetCamera.orthographicSize * 2f) / Screen.height;
            Vector3 worldDelta = new Vector3(screenDelta.x * worldPerPixel, screenDelta.y * worldPerPixel, 0f) * panSensitivity;

            targetPosition += worldDelta;

            // Compute drag velocity for flick/swipe momentum
            Vector3 currentVelocity = worldDelta / Time.unscaledDeltaTime;
            panVelocity = Vector3.Lerp(panVelocity, currentVelocity, 0.4f);

            lastScreenPosition = currentScreenPos;

            // Apply direct transform during finger contact
            if (useBounds)
            {
                targetPosition = ClampPositionToBounds(targetPosition, halfWidth, halfHeight);
            }
            targetPosition.z = transform.position.z;
            transform.position = targetPosition;
        }
        else if (enableInertia && panVelocity.sqrMagnitude > 0.0001f)
        {
            // Apply drag momentum glide
            targetPosition += panVelocity * Time.unscaledDeltaTime;
            panVelocity *= Mathf.Pow(decelerationRate, Time.unscaledDeltaTime * 60f);

            if (useBounds)
            {
                targetPosition = ClampPositionToBounds(targetPosition, halfWidth, halfHeight);
            }
            targetPosition.z = transform.position.z;
            transform.position = targetPosition;
        }
        else
        {
            panVelocity = Vector3.zero;

            if (isCameraFollowingPlayer && worldPosFollow != null)
            {
                // Smoothly ease back towards player using SmoothDamp
                Vector3 desiredPos = worldPosFollow.position + offset;

                if (useBounds)
                {
                    desiredPos = ClampPositionToBounds(desiredPos, halfWidth, halfHeight);
                }
                desiredPos.z = transform.position.z;

                // SmoothDamp ensures a fluid spring-like transition without snapping
                transform.position = Vector3.SmoothDamp(
                    transform.position, 
                    desiredPos, 
                    ref currentSmoothDampVelocity, 
                    followSmoothTime, 
                    Mathf.Infinity, 
                    Time.unscaledDeltaTime
                );

                targetPosition = transform.position;
            }
        }
    }

    private Vector3 ClampPositionToBounds(Vector3 pos, float halfWidth, float halfHeight)
    {
        float minX = minWorldBounds.x + halfWidth;
        float maxX = maxWorldBounds.x - halfWidth;
        float minY = minWorldBounds.y + halfHeight;
        float maxY = maxWorldBounds.y - halfHeight;

        if (minX > maxX)
            pos.x = (minWorldBounds.x + maxWorldBounds.x) * 0.5f;
        else
            pos.x = Mathf.Clamp(pos.x, minX, maxX);

        if (minY > maxY)
            pos.y = (minWorldBounds.y + maxWorldBounds.y) * 0.5f;
        else
            pos.y = Mathf.Clamp(pos.y, minY, maxY);

        return pos;
    }
}