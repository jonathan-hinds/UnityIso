using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public class MouseCameraController : MonoBehaviour
{
    [Header("Zoom")]
    [SerializeField, Min(0.01f)] private float zoomStep = 2f;
    [SerializeField, Min(0.01f)] private float zoomSmoothSpeed = 12f;
    [SerializeField, Min(0.1f)] private float minZoom = 2f;
    [SerializeField, Min(0.1f)] private float maxZoom = 20f;

    [Header("Pan")]
    [SerializeField, Min(0.01f)] private float panSmoothSpeed = 12f;
    [SerializeField] private bool blockWhenPointerOverUi = true;

    private Camera controlledCamera;
    private Plane dragPlane;
    private Vector3 targetPosition;
    private Vector3 dragStartWorldPoint;
    private Vector3 dragStartCameraPosition;
    private float targetZoom;
    private bool isDragging;
    private bool inputEnabled = true;

    public bool InputEnabled => inputEnabled;

    private void Awake()
    {
        controlledCamera = GetComponent<Camera>();
        controlledCamera.orthographic = true;

        dragPlane = new Plane(Vector3.forward, Vector3.zero);
        targetPosition = transform.position;
        targetZoom = controlledCamera.orthographicSize;
        ClampTargets();
    }

    private void OnEnable()
    {
        targetPosition = transform.position;
        if (controlledCamera != null)
        {
            targetZoom = controlledCamera.orthographicSize;
        }
    }

    private void Update()
    {
        if (inputEnabled)
        {
            HandleZoomInput();
            HandlePanInput();
        }

        SmoothCameraMotion();
    }

    public void SetInputEnabled(bool enabled)
    {
        inputEnabled = enabled;
        if (!inputEnabled)
        {
            isDragging = false;
        }
    }

    public void SetTargetPosition(Vector3 worldPosition, bool snapImmediately = false)
    {
        targetPosition = new Vector3(worldPosition.x, worldPosition.y, transform.position.z);
        ClampTargets();

        if (snapImmediately)
        {
            transform.position = targetPosition;
        }
    }

    private void HandleZoomInput()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null)
        {
            return;
        }

        float scrollDelta = mouse.scroll.ReadValue().y;
        if (Mathf.Approximately(scrollDelta, 0f))
        {
            return;
        }

        targetZoom -= Mathf.Sign(scrollDelta) * zoomStep;
        targetZoom = Mathf.Clamp(targetZoom, minZoom, maxZoom);
    }

    private void HandlePanInput()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null)
        {
            isDragging = false;
            return;
        }

        if (mouse.middleButton.wasPressedThisFrame)
        {
            if (blockWhenPointerOverUi && IsPointerOverUi())
            {
                return;
            }

            if (TryGetMouseWorldPoint(out dragStartWorldPoint))
            {
                dragStartCameraPosition = targetPosition;
                isDragging = true;
            }
        }

        if (mouse.middleButton.wasReleasedThisFrame)
        {
            isDragging = false;
        }

        if (!isDragging)
        {
            return;
        }

        if (!TryGetMouseWorldPoint(out Vector3 currentWorldPoint))
        {
            return;
        }

        Vector3 dragOffset = dragStartWorldPoint - currentWorldPoint;
        targetPosition = dragStartCameraPosition + dragOffset;
        targetPosition.z = transform.position.z;
    }

    private void SmoothCameraMotion()
    {
        ClampTargets();

        float zoomLerp = 1f - Mathf.Exp(-zoomSmoothSpeed * Time.unscaledDeltaTime);
        controlledCamera.orthographicSize = Mathf.Lerp(controlledCamera.orthographicSize, targetZoom, zoomLerp);

        float moveLerp = 1f - Mathf.Exp(-panSmoothSpeed * Time.unscaledDeltaTime);
        Vector3 nextPosition = Vector3.Lerp(transform.position, targetPosition, moveLerp);
        nextPosition.z = transform.position.z;
        transform.position = nextPosition;
    }

    private void ClampTargets()
    {
        minZoom = Mathf.Max(0.1f, minZoom);
        maxZoom = Mathf.Max(minZoom, maxZoom);
        targetZoom = Mathf.Clamp(targetZoom, minZoom, maxZoom);
        targetPosition.z = transform.position.z;
    }

    private bool TryGetMouseWorldPoint(out Vector3 worldPoint)
    {
        if (controlledCamera == null || Mouse.current == null)
        {
            worldPoint = default;
            return false;
        }

        Vector2 screenPosition = Mouse.current.position.ReadValue();
        if (!IsFinite(screenPosition))
        {
            worldPoint = default;
            return false;
        }

        Ray ray = controlledCamera.ScreenPointToRay(screenPosition);
        if (dragPlane.Raycast(ray, out float enter))
        {
            worldPoint = ray.GetPoint(enter);
            return true;
        }

        worldPoint = default;
        return false;
    }

    private bool IsPointerOverUi()
    {
        if (EventSystem.current == null)
        {
            return false;
        }

        return EventSystem.current.IsPointerOverGameObject();
    }

    private void OnValidate()
    {
        minZoom = Mathf.Max(0.1f, minZoom);
        maxZoom = Mathf.Max(minZoom, maxZoom);
        zoomStep = Mathf.Max(0.01f, zoomStep);
        zoomSmoothSpeed = Mathf.Max(0.01f, zoomSmoothSpeed);
        panSmoothSpeed = Mathf.Max(0.01f, panSmoothSpeed);
    }

    private static bool IsFinite(Vector2 value)
    {
        return !float.IsNaN(value.x) &&
               !float.IsNaN(value.y) &&
               !float.IsInfinity(value.x) &&
               !float.IsInfinity(value.y);
    }
}
