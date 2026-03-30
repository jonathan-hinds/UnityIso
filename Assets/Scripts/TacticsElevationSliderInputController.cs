using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class TacticsElevationSliderInputController : MonoBehaviour
{
    [SerializeField] private IsometricMapLayerVisibilityController visibilityController;

    private void OnEnable()
    {
        if (visibilityController == null)
        {
            visibilityController = FindFirstObjectByType<IsometricMapLayerVisibilityController>();
        }
    }

    public void AssignVisibilityController(IsometricMapLayerVisibilityController controller)
    {
        visibilityController = controller;
    }

    private void Update()
    {
        if (visibilityController == null)
        {
            visibilityController = FindFirstObjectByType<IsometricMapLayerVisibilityController>();
            if (visibilityController == null)
            {
                return;
            }
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || IsTextInputFocused())
        {
            return;
        }

        if (keyboard.upArrowKey.wasPressedThisFrame)
        {
            visibilityController.TryAdjustVisibleElevation(1);
        }
        else if (keyboard.downArrowKey.wasPressedThisFrame)
        {
            visibilityController.TryAdjustVisibleElevation(-1);
        }
    }

    private static bool IsTextInputFocused()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
        {
            return false;
        }

        GameObject selectedObject = eventSystem.currentSelectedGameObject;
        if (selectedObject == null)
        {
            return false;
        }

        return selectedObject.GetComponent<InputField>() != null;
    }
}
