using UnityEngine;
using UnityEngine.EventSystems;

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
        HandleZoomInput();
        HandlePanInput();
        SmoothCameraMotion();
    }

    private void HandleZoomInput()
    {
        float scrollDelta = Input.mouseScrollDelta.y;
        if (Mathf.Approximately(scrollDelta, 0f))
        {
            return;
        }

        targetZoom -= scrollDelta * zoomStep;
        targetZoom = Mathf.Clamp(targetZoom, minZoom, maxZoom);
    }

    private void HandlePanInput()
    {
        if (Input.GetMouseButtonDown(2))
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

        if (Input.GetMouseButtonUp(2))
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
        Ray ray = controlledCamera.ScreenPointToRay(Input.mousePosition);
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
}
