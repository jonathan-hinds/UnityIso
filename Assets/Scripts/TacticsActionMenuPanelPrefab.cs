using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class TacticsActionMenuPanelPrefab : MonoBehaviour
{
    [SerializeField] private RectTransform root;
    [SerializeField] private Text characterNameText;
    [SerializeField] private Button moveButton;
    [SerializeField] private Button openChestButton;
    [SerializeField] private Button abilitiesButton;
    [SerializeField] private Button endTurnButton;

    public RectTransform Root => root;
    public Text CharacterNameText => characterNameText;
    public Button MoveButton => moveButton;
    public Button OpenChestButton => openChestButton;
    public Button AbilitiesButton => abilitiesButton;
    public Button EndTurnButton => endTurnButton;

    public bool HasRequiredBindings =>
        root != null &&
        characterNameText != null &&
        moveButton != null &&
        openChestButton != null &&
        abilitiesButton != null &&
        endTurnButton != null;

    public void Configure(
        RectTransform boundRoot,
        Text boundCharacterNameText,
        Button boundMoveButton,
        Button boundOpenChestButton,
        Button boundAbilitiesButton,
        Button boundEndTurnButton)
    {
        root = boundRoot;
        characterNameText = boundCharacterNameText;
        moveButton = boundMoveButton;
        openChestButton = boundOpenChestButton;
        abilitiesButton = boundAbilitiesButton;
        endTurnButton = boundEndTurnButton;
    }
}
