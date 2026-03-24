using System;
using UnityEngine;
using UnityEngine.UIElements;

[DisallowMultipleComponent]
[RequireComponent(typeof(UIDocument))]
public sealed class TacticsMainMenuView : MonoBehaviour
{
    private const string PanelSettingsResourcePath = "UI/TacticsMainMenuPanelSettings";
    private const string VisualTreeResourcePath = "UI/TacticsMainMenu";
    private const string RootElementName = "main-menu-root";
    private const string PlayButtonName = "play-button";
    private const string StatusLabelName = "status-label";

    [Header("Assets")]
    [SerializeField] private PanelSettings panelSettings;
    [SerializeField] private VisualTreeAsset visualTreeAsset;

    private UIDocument uiDocument;
    private VisualElement rootElement;
    private Button playButton;
    private Label statusLabel;

    public event Action PlayRequested;

    public bool IsVisible => gameObject.activeSelf;

    private void Awake()
    {
        uiDocument = GetComponent<UIDocument>();
        ResolveAssets();
        ApplyDocumentConfiguration();
        CacheElements();
    }

    private void OnEnable()
    {
        CacheElements();
        RegisterCallbacks();
    }

    private void OnDisable()
    {
        UnregisterCallbacks();
    }

    public void Show()
    {
        gameObject.SetActive(true);
        CacheElements();
        SetInteractable(true);

        if (rootElement != null)
        {
            rootElement.style.display = DisplayStyle.Flex;
        }

        playButton?.Focus();
    }

    public void Hide()
    {
        if (rootElement != null)
        {
            rootElement.style.display = DisplayStyle.None;
        }

        gameObject.SetActive(false);
    }

    public void SetInteractable(bool interactable)
    {
        CacheElements();
        playButton?.SetEnabled(interactable);
    }

    public void SetStatusText(string text)
    {
        CacheElements();
        if (statusLabel == null)
        {
            return;
        }

        statusLabel.text = text ?? string.Empty;
        statusLabel.style.display = string.IsNullOrWhiteSpace(statusLabel.text)
            ? DisplayStyle.None
            : DisplayStyle.Flex;
    }

    private void ResolveAssets()
    {
        panelSettings ??= Resources.Load<PanelSettings>(PanelSettingsResourcePath);
        visualTreeAsset ??= Resources.Load<VisualTreeAsset>(VisualTreeResourcePath);
    }

    private void ApplyDocumentConfiguration()
    {
        if (uiDocument == null)
        {
            return;
        }

        if (panelSettings != null)
        {
            uiDocument.panelSettings = panelSettings;
        }

        if (visualTreeAsset != null)
        {
            uiDocument.visualTreeAsset = visualTreeAsset;
        }
    }

    private void CacheElements()
    {
        if (uiDocument == null || uiDocument.rootVisualElement == null)
        {
            return;
        }

        rootElement = uiDocument.rootVisualElement.Q<VisualElement>(RootElementName) ?? uiDocument.rootVisualElement;
        playButton = rootElement.Q<Button>(PlayButtonName);
        statusLabel = rootElement.Q<Label>(StatusLabelName);
    }

    private void RegisterCallbacks()
    {
        if (playButton == null)
        {
            return;
        }

        playButton.clicked -= HandlePlayButtonClicked;
        playButton.clicked += HandlePlayButtonClicked;
    }

    private void UnregisterCallbacks()
    {
        if (playButton == null)
        {
            return;
        }

        playButton.clicked -= HandlePlayButtonClicked;
    }

    private void HandlePlayButtonClicked()
    {
        PlayRequested?.Invoke();
    }
}
