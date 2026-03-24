using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class TacticsCharacterElevationVisibility : MonoBehaviour
{
    [SerializeField] private IsometricMapLayerVisibilityController visibilityController;
    [SerializeField] private TacticsCharacterController characterController;
    [SerializeField] private TacticsCharacterAnimator characterAnimator;

    private readonly List<ColliderState> colliderStates = new();
    private readonly List<RendererState> rendererStates = new();
    private int lastKnownElevation = int.MinValue;
    private bool hasAppliedVisibility;
    private bool isVisible = true;

    public bool IsVisible => isVisible;

    private void Awake()
    {
        ResolveDependencies();
        CacheRendererStates();
        CacheColliderStates();
    }

    private void OnEnable()
    {
        ResolveDependencies();
        CacheRendererStates();
        CacheColliderStates();
        AssignVisibilityController(visibilityController != null
            ? visibilityController
            : FindFirstObjectByType<IsometricMapLayerVisibilityController>());
        RefreshVisibility(force: true);
    }

    private void Update()
    {
        if (characterController == null)
        {
            ResolveDependencies();
            return;
        }

        if (visibilityController == null)
        {
            AssignVisibilityController(FindFirstObjectByType<IsometricMapLayerVisibilityController>());
        }

        int currentElevation = characterController.CurrentElevation;
        if (!hasAppliedVisibility || currentElevation != lastKnownElevation)
        {
            RefreshVisibility(force: true);
        }
    }

    private void OnDisable()
    {
        if (visibilityController != null)
        {
            visibilityController.VisibilityChanged -= HandleVisibilityChanged;
        }
    }

    public void AssignVisibilityController(IsometricMapLayerVisibilityController controller)
    {
        if (ReferenceEquals(visibilityController, controller))
        {
            return;
        }

        if (visibilityController != null)
        {
            visibilityController.VisibilityChanged -= HandleVisibilityChanged;
        }

        visibilityController = controller;

        if (visibilityController != null)
        {
            visibilityController.VisibilityChanged -= HandleVisibilityChanged;
            visibilityController.VisibilityChanged += HandleVisibilityChanged;
        }
    }

    private void HandleVisibilityChanged(int _, int __)
    {
        RefreshVisibility(force: true);
    }

    private void RefreshVisibility(bool force = false)
    {
        ResolveDependencies();

        if (characterController == null)
        {
            return;
        }

        int currentElevation = characterController.CurrentElevation;
        bool shouldBeVisible = visibilityController == null || currentElevation <= visibilityController.VisibleElevation;

        if (!force && hasAppliedVisibility && isVisible == shouldBeVisible && lastKnownElevation == currentElevation)
        {
            return;
        }

        lastKnownElevation = currentElevation;
        hasAppliedVisibility = true;
        ApplyVisibility(shouldBeVisible);
    }

    private void ApplyVisibility(bool shouldBeVisible)
    {
        isVisible = shouldBeVisible;
        characterController?.SetPresentationVisible(shouldBeVisible);
        characterAnimator?.SetVisualVisibility(shouldBeVisible);

        CacheRendererStates();
        for (int i = 0; i < rendererStates.Count; i++)
        {
            RendererState state = rendererStates[i];
            if (state.Renderer == null)
            {
                continue;
            }

            state.Renderer.enabled = shouldBeVisible && state.WasEnabled;
        }

        for (int i = 0; i < colliderStates.Count; i++)
        {
            ColliderState state = colliderStates[i];
            if (state.Collider == null)
            {
                continue;
            }

            state.Collider.enabled = shouldBeVisible && state.WasEnabled;
        }
    }

    private void ResolveDependencies()
    {
        characterController ??= GetComponent<TacticsCharacterController>();
        characterAnimator ??= GetComponent<TacticsCharacterAnimator>();

        if (characterAnimator == null)
        {
            characterAnimator = GetComponentInChildren<TacticsCharacterAnimator>(true);
        }
    }

    private void CacheRendererStates()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || ContainsRenderer(renderer))
            {
                continue;
            }

            rendererStates.Add(new RendererState(renderer, renderer.enabled));
        }
    }

    private void CacheColliderStates()
    {
        Collider2D[] colliders = GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D collider = colliders[i];
            if (collider == null || ContainsCollider(collider))
            {
                continue;
            }

            colliderStates.Add(new ColliderState(collider, collider.enabled));
        }
    }

    private bool ContainsRenderer(Renderer renderer)
    {
        for (int i = 0; i < rendererStates.Count; i++)
        {
            if (rendererStates[i].Renderer == renderer)
            {
                return true;
            }
        }

        return false;
    }

    private bool ContainsCollider(Collider2D collider)
    {
        for (int i = 0; i < colliderStates.Count; i++)
        {
            if (colliderStates[i].Collider == collider)
            {
                return true;
            }
        }

        return false;
    }

    private readonly struct ColliderState
    {
        public ColliderState(Collider2D collider, bool wasEnabled)
        {
            Collider = collider;
            WasEnabled = wasEnabled;
        }

        public Collider2D Collider { get; }
        public bool WasEnabled { get; }
    }

    private readonly struct RendererState
    {
        public RendererState(Renderer renderer, bool wasEnabled)
        {
            Renderer = renderer;
            WasEnabled = wasEnabled;
        }

        public Renderer Renderer { get; }
        public bool WasEnabled { get; }
    }
}
