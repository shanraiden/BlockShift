using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Camera))]
public class SmoothCameraPan2D : MonoBehaviour
{
    [Header("Input Action Reference")]
    [Tooltip("Reference to an Input Action for screen contact/press (e.g. Touch/press or Mouse/leftButton)")]
    [SerializeField] private InputActionProperty touchContactAction;

    [Tooltip("Reference to an Input Action for screen position (e.g. Touch/position or Mouse/position)")]
    [SerializeField] private InputActionProperty touchPositionAction;

    [Header("Pan Settings")]
    [Tooltip("Multiplier for camera panning speed.")]
    [SerializeField] private float panSensitivity = 1.0f;

    [Tooltip("Smoothness factor for camera movement (higher = faster response, lower = smoother/drag).")]
    [SerializeField] private float smoothSpeed = 10f;

    [Header("Optional Bounds Clamping")]
    [SerializeField] private bool enableBounds = false;
    [SerializeField] private Vector2 minBounds = new Vector2(-10f, -10f);
    [SerializeField] private Vector2 maxBounds = new Vector2(10f, 10f);

    private Camera cam;
    private Vector3 targetPosition;
    private Vector3 dragOriginWorld;
    private bool isDragging = false;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        targetPosition = transform.position;
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

    private void OnDragStart(InputAction.CallbackContext context)
    {
        isDragging = true;
        Vector2 screenPos = touchPositionAction.action.ReadValue<Vector2>();

        // Record where in the world space the drag started
        dragOriginWorld = GetWorldPoint(screenPos);
    }

    private void OnDragEnd(InputAction.CallbackContext context)
    {
        isDragging = false;
    }

    private void LateUpdate()
    {
        if (isDragging)
        {
            Vector2 currentScreenPos = touchPositionAction.action.ReadValue<Vector2>();
            Vector3 currentWorldPos = GetWorldPoint(currentScreenPos);

            // Calculate world delta between initial drag origin and current pointer position
            Vector3 difference = dragOriginWorld - currentWorldPos;

            // Update target camera position
            targetPosition += difference * panSensitivity;

            // Apply optional world bounds clamping
            if (enableBounds)
            {
                targetPosition.x = Mathf.Clamp(targetPosition.x, minBounds.x, maxBounds.x);
                targetPosition.y = Mathf.Clamp(targetPosition.y, minBounds.y, maxBounds.y);
            }

            // Keep Z position intact
            targetPosition.z = transform.position.z;
        }

        // Smoothly interpolate current camera position towards target position
        transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * smoothSpeed);
    }

    private Vector3 GetWorldPoint(Vector2 screenPosition)
    {
        // Convert screen coordinates to world coordinates relative to camera distance
        Vector3 point = new Vector3(screenPosition.x, screenPosition.y, Mathf.Abs(cam.transform.position.z));
        return cam.ScreenToWorldPoint(point);
    }

    private void OnDrawGizmosSelected()
    {
        if (!enableBounds) return;

        Gizmos.color = Color.yellow;
        Vector3 center = new Vector3((minBounds.x + maxBounds.x) * 0.5f, (minBounds.y + maxBounds.y) * 0.5f, 0f);
        Vector3 size = new Vector3(maxBounds.x - minBounds.x, maxBounds.y - minBounds.y, 1f);
        Gizmos.DrawWireCube(center, size);
    }
}