using UnityEngine;
using UnityEngine.UI;

public sealed class TacticsInventoryPanelPrefab : MonoBehaviour
{
    [SerializeField] private RectTransform root;
    [SerializeField] private Button dismissButton;
    [SerializeField] private Text titleText;
    [SerializeField] private Text subtitleText;
    [SerializeField] private Text emptyStateText;
    [SerializeField] private RectTransform characterListRoot;
    [SerializeField] private RectTransform equipmentRoot;
    [SerializeField] private RectTransform inventoryContentRoot;

    public RectTransform Root => root;
    public Button DismissButton => dismissButton;
    public Text TitleText => titleText;
    public Text SubtitleText => subtitleText;
    public Text EmptyStateText => emptyStateText;
    public RectTransform CharacterListRoot => characterListRoot;
    public RectTransform EquipmentRoot => equipmentRoot;
    public RectTransform InventoryContentRoot => inventoryContentRoot;

    public bool HasRequiredBindings =>
        root != null &&
        dismissButton != null &&
        titleText != null &&
        subtitleText != null &&
        emptyStateText != null &&
        characterListRoot != null &&
        equipmentRoot != null &&
        inventoryContentRoot != null;

    public void Configure(
        RectTransform boundRoot,
        Button boundDismissButton,
        Text boundTitleText,
        Text boundSubtitleText,
        Text boundEmptyStateText,
        RectTransform boundCharacterListRoot,
        RectTransform boundEquipmentRoot,
        RectTransform boundInventoryContentRoot)
    {
        root = boundRoot;
        dismissButton = boundDismissButton;
        titleText = boundTitleText;
        subtitleText = boundSubtitleText;
        emptyStateText = boundEmptyStateText;
        characterListRoot = boundCharacterListRoot;
        equipmentRoot = boundEquipmentRoot;
        inventoryContentRoot = boundInventoryContentRoot;
    }
}
