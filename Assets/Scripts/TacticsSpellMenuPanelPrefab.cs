using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class TacticsSpellMenuPanelPrefab : MonoBehaviour
{
    [SerializeField] private RectTransform root;
    [SerializeField] private Button dismissButton;
    [SerializeField] private Text titleText;
    [SerializeField] private Text subtitleText;
    [SerializeField] private Text emptyStateText;
    [SerializeField] private RectTransform contentRoot;

    public RectTransform Root => root;
    public Button DismissButton => dismissButton;
    public Text TitleText => titleText;
    public Text SubtitleText => subtitleText;
    public Text EmptyStateText => emptyStateText;
    public RectTransform ContentRoot => contentRoot;

    public bool HasRequiredBindings =>
        root != null &&
        dismissButton != null &&
        titleText != null &&
        subtitleText != null &&
        emptyStateText != null &&
        contentRoot != null;

    public void Configure(
        RectTransform boundRoot,
        Button boundDismissButton,
        Text boundTitleText,
        Text boundSubtitleText,
        Text boundEmptyStateText,
        RectTransform boundContentRoot)
    {
        root = boundRoot;
        dismissButton = boundDismissButton;
        titleText = boundTitleText;
        subtitleText = boundSubtitleText;
        emptyStateText = boundEmptyStateText;
        contentRoot = boundContentRoot;
    }
}
