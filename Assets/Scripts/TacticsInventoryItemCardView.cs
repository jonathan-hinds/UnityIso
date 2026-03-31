using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class TacticsInventoryItemCardView : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private RectTransform root;
    [SerializeField] private Button button;
    [SerializeField] private Image iconImage;
    [SerializeField] private Text stackCountText;
    [SerializeField] private GameObject equippedIndicator;
    [SerializeField] private TacticsAbilityTooltipTrigger tooltipTrigger;

    private Action<PointerEventData> rightClickAction;

    public void Configure(
        RectTransform boundRoot,
        Button boundButton,
        Image boundIconImage,
        Text boundStackCountText,
        GameObject boundEquippedIndicator,
        TacticsAbilityTooltipTrigger boundTooltipTrigger)
    {
        root = boundRoot;
        button = boundButton;
        iconImage = boundIconImage;
        stackCountText = boundStackCountText;
        equippedIndicator = boundEquippedIndicator;
        tooltipTrigger = boundTooltipTrigger;
    }

    public void Bind(
        Sprite icon,
        int quantity,
        bool equipped,
        Action onLeftClick,
        Action<PointerEventData> onRightClick,
        Action<PointerEventData> onPointerEnter,
        Action<PointerEventData> onPointerExit,
        Action<PointerEventData> onPointerMove)
    {
        if (root == null)
        {
            return;
        }

        root.gameObject.SetActive(true);
        rightClickAction = onRightClick;

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            if (onLeftClick != null)
            {
                button.onClick.AddListener(() => onLeftClick());
            }
        }

        if (iconImage != null)
        {
            iconImage.sprite = icon;
            iconImage.enabled = icon != null;
        }

        if (stackCountText != null)
        {
            bool showStackCount = quantity > 1 && icon != null;
            stackCountText.gameObject.SetActive(showStackCount);
            stackCountText.text = showStackCount ? quantity.ToString() : string.Empty;
        }

        if (equippedIndicator != null)
        {
            equippedIndicator.SetActive(equipped);
        }

        tooltipTrigger?.Initialize(onPointerEnter, onPointerExit, onPointerMove);
    }

    public void BindEmpty()
    {
        Bind(null, 0, false, null, null, null, null, null);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData != null && eventData.button == PointerEventData.InputButton.Right)
        {
            rightClickAction?.Invoke(eventData);
        }
    }
}
