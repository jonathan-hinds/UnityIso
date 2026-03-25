using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class TacticsTurnCameraDirector : MonoBehaviour
{
    [SerializeField] private MouseCameraController cameraController;
    [SerializeField] private Camera targetCamera;
    [SerializeField, Min(0.05f)] private float focusDuration = 0.4f;
    [SerializeField, Min(0f)] private float focusTolerance = 0.05f;
    [SerializeField, Min(0f)] private float immediateSnapDistance = 0.2f;
    [SerializeField] private Vector2 focusOffset = new Vector2(0f, 0.25f);

    public bool IsFocusing { get; private set; }

    private void Awake()
    {
        ResolveReferences();
    }

    public IEnumerator FocusOnWorldPoint(Vector3 worldPoint)
    {
        ResolveReferences();
        if (cameraController == null || targetCamera == null)
        {
            yield break;
        }

        Vector3 focusPoint = new Vector3(worldPoint.x + focusOffset.x, worldPoint.y + focusOffset.y, targetCamera.transform.position.z);
        bool restoreInput = cameraController.InputEnabled;
        float focusToleranceSqr = focusTolerance * focusTolerance;
        float immediateSnapDistanceSqr = immediateSnapDistance * immediateSnapDistance;

        IsFocusing = true;
        cameraController.SetInputEnabled(false);

        if (Vector2.SqrMagnitude((Vector2)(cameraController.TargetPosition - focusPoint)) <= focusToleranceSqr ||
            Vector2.SqrMagnitude((Vector2)(targetCamera.transform.position - focusPoint)) <= immediateSnapDistanceSqr)
        {
            cameraController.SetTargetPosition(focusPoint, snapImmediately: true);
            cameraController.SetInputEnabled(restoreInput);
            IsFocusing = false;
            yield break;
        }

        cameraController.SetTargetPosition(focusPoint);

        float elapsed = 0f;
        while (elapsed < focusDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            if (Vector2.SqrMagnitude((Vector2)(targetCamera.transform.position - focusPoint)) <= focusToleranceSqr)
            {
                break;
            }

            yield return null;
        }

        cameraController.SetTargetPosition(focusPoint);
        cameraController.SetInputEnabled(restoreInput);
        IsFocusing = false;
    }

    private void ResolveReferences()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (cameraController == null && targetCamera != null)
        {
            cameraController = targetCamera.GetComponent<MouseCameraController>();
        }
    }
}
