using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Camera))]
public class CameraConfig : MonoBehaviour
{
    [Header("Scaling & Orientation Settings")]
    [Tooltip("Target scene width when the phone is in Landscape mode.")]
    public float landscapeSceneWidth = 15.0f;
    [Tooltip("Target scene width when the phone is in Portrait mode.")]
    public float portraitSceneWidth = 10.0f;

    [Header("Follow Settings")]
    [Tooltip("Drag your player Transform here.")]
    public Transform worldPosFollow;
    [Tooltip("Time in seconds for camera to reach player. Smaller = snappier, Larger = smoother.")]
    public float followSmoothTime = 0.2f;

    [Header("Offset Settings")]
    [Tooltip("Offset when player is on the LEFT side of the scene center.")]
    public Vector3 leftSideOffset = new Vector3(-2f, 0f, -10f);
    [Tooltip("Offset when player is on the RIGHT side of the scene center.")]
    public Vector3 rightSideOffset = new Vector3(2f, 0f, -10f);
    public bool isCameraFollowingPlayer = true;

    [Header("Pan / Drag Settings (New Input System)")]
    [SerializeField] private InputActionProperty touchContactAction;
    [SerializeField] private InputActionProperty touchPositionAction;
    [SerializeField] private float panSensitivity = 1.0f;

    [Header("Inertia / Glide Settings")]
    public bool enableInertia = true;
    [Range(0.80f, 0.99f)]
    public float decelerationRate = 0.92f;

    [Header("Boundary Settings")]
    public bool useBounds = true;
    public RectTransform boundaryRectTransform;

    private Vector2 minWorldBounds;
    private Vector2 maxWorldBounds;

    // Internal State
    private Camera targetCamera;
    private Vector3 targetPosition;
    private Vector2 lastScreenPosition;
    private Vector3 panVelocity;
    private Vector3 currentSmoothDampVelocity;
    private bool isDragging = false;

    [Header("Target & Center Reference")]
    [SerializeField] private Transform playerTarget;
    [SerializeField] private float offsetTransitionSpeed = 3f;
    private Rigidbody2D playerRb;
    private Vector3 currentOffset;

    private void Awake()
    {
        targetCamera = GetComponent<Camera>();
        targetPosition = transform.position;

        if (playerTarget != null)
        {
            playerTarget.TryGetComponent<Rigidbody2D>(out playerRb);
        }

        FetchWorldBoundsFromRect();
    }

    private void Start()
    {
        // Force camera to start inside bounds immediately upon game launch
        if (useBounds)
        {
            bool isLandscape = Screen.width >= Screen.height;
            float activeSceneWidth = isLandscape ? landscapeSceneWidth : portraitSceneWidth;
            float unitsPerPixel = activeSceneWidth / Screen.width;
            float halfHeight = 0.5f * unitsPerPixel * Screen.height;
            targetCamera.orthographicSize = halfHeight;
            float halfWidth = halfHeight * targetCamera.aspect;

            transform.position = ClampPositionToBounds(transform.position, halfWidth, halfHeight);
            targetPosition = transform.position;
        }
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

    /// <summary>
    /// Checks player position relative to scene center and transitions the offset.
    /// </summary>
    public void UpdateCameraOffset()
    {
        if (playerRb == null) return;

        float vx = playerRb.linearVelocity.x;
        Vector3 targetOffset;

        if (Mathf.Abs(vx) <= 0.01f)
        {
            targetOffset = Vector3.zero;
        }
        else if (vx < 0)
        {
            targetOffset = leftSideOffset;
        }
        else
        {
            targetOffset = rightSideOffset;
        }

        currentOffset = Vector3.Lerp(currentOffset, targetOffset, offsetTransitionSpeed * Time.deltaTime);
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
        // 1. Automatic Orientation & Width Sizing:
        bool isLandscape = Screen.width >= Screen.height;
        float activeSceneWidth = isLandscape ? landscapeSceneWidth : portraitSceneWidth;

        float unitsPerPixel = activeSceneWidth / Screen.width;
        float halfHeight = 0.5f * unitsPerPixel * Screen.height;
        targetCamera.orthographicSize = halfHeight;

        float halfWidth = halfHeight * targetCamera.aspect;

        // 2. Process Movement Mode
        if (isDragging)
        {
            Vector2 currentScreenPos = touchPositionAction.action.ReadValue<Vector2>();
            Vector2 screenDelta = lastScreenPosition - currentScreenPos;

            float worldPerPixel = (targetCamera.orthographicSize * 2f) / Screen.height;
            Vector3 worldDelta = new Vector3(screenDelta.x * worldPerPixel, screenDelta.y * worldPerPixel, 0f) * panSensitivity;

            targetPosition += worldDelta;

            Vector3 currentVelocity = worldDelta / Time.unscaledDeltaTime;
            panVelocity = Vector3.Lerp(panVelocity, currentVelocity, 0.4f);

            lastScreenPosition = currentScreenPos;

            if (useBounds)
            {
                targetPosition = ClampPositionToBounds(targetPosition, halfWidth, halfHeight);
            }
            targetPosition.z = transform.position.z;
            transform.position = targetPosition;
        }
        else if (enableInertia && panVelocity.sqrMagnitude > 0.0001f)
        {
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
                UpdateCameraOffset();
                Vector3 desiredPos = worldPosFollow.position + currentOffset;

                if (useBounds)
                {
                    desiredPos = ClampPositionToBounds(desiredPos, halfWidth, halfHeight);
                }
                desiredPos.z = transform.position.z;

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