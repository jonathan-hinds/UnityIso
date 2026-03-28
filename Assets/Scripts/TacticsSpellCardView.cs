using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class TacticsSpellCardView : MonoBehaviour
{
    [SerializeField] private RectTransform root;
    [SerializeField] private Button button;
    [SerializeField] private Text nameText;
    [SerializeField] private Text headerSummaryText;
    [SerializeField] private Text metaText;
    [SerializeField] private Text descriptionText;
    [SerializeField] private GameObject generatedGroup;
    [SerializeField] private GameObject generatedDivider;
    [SerializeField] private Text generatedText;
    [SerializeField] private GameObject statusGroup;
    [SerializeField] private Text statusText;
    [SerializeField] private GameObject selectedIndicator;
    [SerializeField] private TacticsAbilityTooltipTrigger tooltipTrigger;

    public RectTransform Root => root;
    public bool HasRequiredBindings =>
        root != null &&
        button != null &&
        nameText != null &&
        headerSummaryText != null &&
        metaText != null &&
        descriptionText != null &&
        generatedText != null &&
        statusText != null &&
        tooltipTrigger != null;

    public void Configure(
        RectTransform boundRoot,
        Button boundButton,
        Text boundNameText,
        Text boundHeaderSummaryText,
        Text boundMetaText,
        Text boundDescriptionText,
        GameObject boundGeneratedGroup,
        GameObject boundGeneratedDivider,
        Text boundGeneratedText,
        GameObject boundStatusGroup,
        Text boundStatusText,
        GameObject boundSelectedIndicator,
        TacticsAbilityTooltipTrigger boundTooltipTrigger)
    {
        root = boundRoot;
        button = boundButton;
        nameText = boundNameText;
        headerSummaryText = boundHeaderSummaryText;
        metaText = boundMetaText;
        descriptionText = boundDescriptionText;
        generatedGroup = boundGeneratedGroup;
        generatedDivider = boundGeneratedDivider;
        generatedText = boundGeneratedText;
        statusGroup = boundStatusGroup;
        statusText = boundStatusText;
        selectedIndicator = boundSelectedIndicator;
        tooltipTrigger = boundTooltipTrigger;
    }

    public void Bind(
        TacticsAbilityCardContent content,
        bool interactable,
        bool isSelected,
        Action onClick,
        Action<PointerEventData> onPointerEnter,
        Action<PointerEventData> onPointerExit,
        Action<PointerEventData> onPointerMove)
    {
        if (!HasRequiredBindings)
        {
            return;
        }

        root.gameObject.SetActive(true);
        nameText.text = string.IsNullOrWhiteSpace(content.Title) ? "ABILITY" : content.Title;
        headerSummaryText.text = content.HeaderCombatSummary;
        headerSummaryText.gameObject.SetActive(!string.IsNullOrWhiteSpace(content.HeaderCombatSummary));
        metaText.text = string.IsNullOrWhiteSpace(content.Cost) && string.IsNullOrWhiteSpace(content.Range)
            ? "No data"
            : $"{content.Cost}    |    {content.Range}";
        descriptionText.text = string.IsNullOrWhiteSpace(content.Description) ? "No description." : content.Description;
        generatedText.text = content.GeneratedDescription;
        statusText.text = content.Status;

        if (generatedGroup != null)
        {
            generatedGroup.SetActive(content.HasGeneratedDescription);
        }
        else
        {
            generatedText.gameObject.SetActive(content.HasGeneratedDescription);
        }

        if (generatedDivider != null)
        {
            generatedDivider.SetActive(content.HasGeneratedDescription);
        }

        if (statusGroup != null)
        {
            statusGroup.SetActive(content.HasStatus);
        }
        else
        {
            statusText.gameObject.SetActive(content.HasStatus);
        }

        if (selectedIndicator != null)
        {
            selectedIndicator.SetActive(isSelected);
        }

        button.onClick.RemoveAllListeners();
        if (onClick != null)
        {
            button.onClick.AddListener(() => onClick());
        }

        button.interactable = interactable;
        tooltipTrigger.Initialize(onPointerEnter, onPointerExit, onPointerMove);
    }

    public void Clear()
    {
        if (root != null)
        {
            root.gameObject.SetActive(false);
        }

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
        }

        tooltipTrigger?.Initialize(null, null, null);
    }
}
