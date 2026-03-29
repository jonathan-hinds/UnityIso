using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class TacticsScreenFadeView : MonoBehaviour
{
    [SerializeField, Min(1000)] private int sortingOrder = 9000;
    [SerializeField] private Color fadeColor = Color.black;

    private Canvas rootCanvas;
    private CanvasGroup canvasGroup;
    private Image fadeImage;

    private void Awake()
    {
        EnsureBuilt();
        SetAlpha(0f);
    }

    public IEnumerator FadeOut(float duration)
    {
        yield return Fade(0f, 1f, duration);
    }

    public IEnumerator FadeIn(float duration)
    {
        yield return Fade(1f, 0f, duration);
    }

    public void SetBlack()
    {
        EnsureBuilt();
        SetAlpha(1f);
    }

    public void Clear()
    {
        EnsureBuilt();
        SetAlpha(0f);
    }

    private IEnumerator Fade(float startAlpha, float targetAlpha, float duration)
    {
        EnsureBuilt();
        duration = Mathf.Max(0.01f, duration);
        float elapsed = 0f;
        SetAlpha(startAlpha);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            SetAlpha(Mathf.Lerp(startAlpha, targetAlpha, t));
            yield return null;
        }

        SetAlpha(targetAlpha);
    }

    private void EnsureBuilt()
    {
        rootCanvas = rootCanvas != null ? rootCanvas : GetComponent<Canvas>();
        if (rootCanvas == null)
        {
            rootCanvas = gameObject.AddComponent<Canvas>();
        }

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        if (scaler == null)
        {
            scaler = gameObject.AddComponent<CanvasScaler>();
        }

        GraphicRaycaster raycaster = GetComponent<GraphicRaycaster>();
        if (raycaster == null)
        {
            raycaster = gameObject.AddComponent<GraphicRaycaster>();
        }

        rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        rootCanvas.sortingOrder = sortingOrder;

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        canvasGroup = canvasGroup != null ? canvasGroup : GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        Transform imageTransform = transform.Find("Fade");
        if (imageTransform == null)
        {
            GameObject imageObject = new GameObject("Fade", typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(transform, false);
            imageTransform = imageObject.transform;
        }

        RectTransform rectTransform = imageTransform as RectTransform;
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;

        fadeImage = imageTransform.GetComponent<Image>();
        fadeImage.color = fadeColor;
        fadeImage.raycastTarget = true;
    }

    private void SetAlpha(float alpha)
    {
        canvasGroup.alpha = Mathf.Clamp01(alpha);
        canvasGroup.blocksRaycasts = canvasGroup.alpha > 0.001f;
        canvasGroup.interactable = false;
    }
}
