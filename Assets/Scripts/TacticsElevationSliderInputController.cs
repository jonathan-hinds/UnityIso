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

[DisallowMultipleComponent]
public sealed class TacticsElevationFocusTracker : MonoBehaviour
{
    [SerializeField] private IsometricMapLayerVisibilityController visibilityController;
    [SerializeField] private TacticsTurnManager turnManager;

    private TacticsCharacterController focusedCharacter;
    private int lastAppliedElevation = int.MinValue;

    private void OnEnable()
    {
        ResolveDependencies();
        SubscribeToTurnManager();
        RefreshFocusedCharacter();
        ApplyFocusElevation(force: true);
    }

    private void OnDisable()
    {
        if (turnManager != null)
        {
            turnManager.ActiveParticipantChanged -= HandleActiveParticipantChanged;
        }
    }

    private void Update()
    {
        ResolveDependencies();
        RefreshFocusedCharacter();
        ApplyFocusElevation(force: false);
    }

    public void AssignVisibilityController(IsometricMapLayerVisibilityController controller)
    {
        visibilityController = controller;
        ApplyFocusElevation(force: true);
    }

    public void AssignTurnManager(TacticsTurnManager manager)
    {
        if (turnManager != null)
        {
            turnManager.ActiveParticipantChanged -= HandleActiveParticipantChanged;
        }

        turnManager = manager;
        SubscribeToTurnManager();
        RefreshFocusedCharacter();
        ApplyFocusElevation(force: true);
    }

    private void HandleActiveParticipantChanged(ITacticsTurnParticipant participant)
    {
        focusedCharacter = participant as TacticsCharacterController;
        if (focusedCharacter != null && !focusedCharacter.IsPlayerControlled)
        {
            focusedCharacter = null;
        }

        ApplyFocusElevation(force: true);
    }

    private void ResolveDependencies()
    {
        visibilityController ??= FindFirstObjectByType<IsometricMapLayerVisibilityController>();
        if (turnManager == null)
        {
            AssignTurnManager(FindFirstObjectByType<TacticsTurnManager>());
        }
    }

    private void SubscribeToTurnManager()
    {
        if (turnManager == null)
        {
            return;
        }

        turnManager.ActiveParticipantChanged -= HandleActiveParticipantChanged;
        turnManager.ActiveParticipantChanged += HandleActiveParticipantChanged;
    }

    private void RefreshFocusedCharacter()
    {
        if (turnManager != null &&
            turnManager.ActiveCharacter != null &&
            turnManager.ActiveCharacter.IsPlayerControlled)
        {
            focusedCharacter = turnManager.ActiveCharacter;
            return;
        }

        if (focusedCharacter != null && focusedCharacter.IsPlayerControlled)
        {
            return;
        }

        TacticsCharacterController[] characters = FindObjectsByType<TacticsCharacterController>(FindObjectsSortMode.None);
        for (int i = 0; i < characters.Length; i++)
        {
            TacticsCharacterController character = characters[i];
            if (character != null && character.IsPlayerControlled)
            {
                focusedCharacter = character;
                return;
            }
        }

        focusedCharacter = null;
    }

    private void ApplyFocusElevation(bool force)
    {
        if (visibilityController == null)
        {
            return;
        }

        int nextElevation = focusedCharacter != null
            ? Mathf.Max(1, focusedCharacter.CurrentElevation)
            : visibilityController.VisibleElevation;

        if (!force && nextElevation == lastAppliedElevation)
        {
            return;
        }

        lastAppliedElevation = nextElevation;
        visibilityController.SetFocusElevation(nextElevation);
    }
}
