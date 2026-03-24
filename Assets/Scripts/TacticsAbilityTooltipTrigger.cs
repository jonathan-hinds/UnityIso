using System;
using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public sealed class TacticsAbilityTooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
{
    private Action<PointerEventData> pointerEnterHandler;
    private Action<PointerEventData> pointerExitHandler;
    private Action<PointerEventData> pointerMoveHandler;

    public void Initialize(
        Action<PointerEventData> onPointerEnter,
        Action<PointerEventData> onPointerExit,
        Action<PointerEventData> onPointerMove)
    {
        pointerEnterHandler = onPointerEnter;
        pointerExitHandler = onPointerExit;
        pointerMoveHandler = onPointerMove;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        pointerEnterHandler?.Invoke(eventData);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        pointerExitHandler?.Invoke(eventData);
    }

    public void OnPointerMove(PointerEventData eventData)
    {
        pointerMoveHandler?.Invoke(eventData);
    }

    private void OnDisable()
    {
        pointerExitHandler?.Invoke(null);
    }
}
