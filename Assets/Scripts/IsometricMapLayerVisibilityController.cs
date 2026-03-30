using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class IsometricMapLayerVisibilityController : MonoBehaviour
{
    [SerializeField] private ProceduralIsometricMapGenerator mapGenerator;
    [SerializeField, Range(0f, 1f)] private float sliceCapAlpha = 0f;

    private readonly List<IsometricMapElevationElement> elevationElements = new();
    private readonly List<IsometricDebrisOverlayController> debrisOverlayControllers = new();
    private readonly List<IsometricFakeShadowOverlayController> fakeShadowOverlayControllers = new();
    private int maximumElevation = 1;
    private int visibleElevation = 0;
    private int focusElevation = 1;

    public event Action<int, int> VisibilityChanged;

    public int MaximumElevation => maximumElevation;
    public int VisibleElevation => visibleElevation;
    public int FocusElevation => focusElevation;

    private void OnEnable()
    {
        if (mapGenerator == null)
        {
            mapGenerator = FindFirstObjectByType<ProceduralIsometricMapGenerator>();
        }

        SubscribeToMapGenerator();

        if (mapGenerator != null && mapGenerator.HasGeneratedMap)
        {
            RefreshFromMap();
        }
    }

    private void OnDisable()
    {
        UnsubscribeFromMapGenerator();
    }

    public void AssignMapGenerator(ProceduralIsometricMapGenerator generator)
    {
        if (ReferenceEquals(mapGenerator, generator))
        {
            if (mapGenerator != null && mapGenerator.HasGeneratedMap)
            {
                RefreshFromMap();
            }

            return;
        }

        UnsubscribeFromMapGenerator();
        mapGenerator = generator;
        SubscribeToMapGenerator();

        if (mapGenerator != null && mapGenerator.HasGeneratedMap)
        {
            RefreshFromMap();
        }
    }

    public void SetVisibleElevation(int elevation)
    {
        int clampedElevation = Mathf.Clamp(elevation, 1, maximumElevation);
        if (visibleElevation == clampedElevation && elevationElements.Count > 0)
        {
            return;
        }

        visibleElevation = clampedElevation;
        ApplyVisibility();
        VisibilityChanged?.Invoke(visibleElevation, maximumElevation);
    }

    public bool TryAdjustVisibleElevation(int delta)
    {
        if (delta == 0)
        {
            return false;
        }

        int nextElevation = Mathf.Clamp(visibleElevation + delta, 1, maximumElevation);
        if (nextElevation == visibleElevation)
        {
            return false;
        }

        SetVisibleElevation(nextElevation);
        return true;
    }

    public void SetFocusElevation(int elevation)
    {
        int clampedElevation = Mathf.Clamp(elevation, 1, maximumElevation);
        if (focusElevation == clampedElevation && fakeShadowOverlayControllers.Count > 0)
        {
            return;
        }

        focusElevation = clampedElevation;
        ApplyVisibility();
    }

    private void HandleMapGenerated()
    {
        RefreshFromMap();
    }

    private void RefreshFromMap()
    {
        elevationElements.Clear();
        debrisOverlayControllers.Clear();
        fakeShadowOverlayControllers.Clear();

        if (mapGenerator == null)
        {
            maximumElevation = 1;
            visibleElevation = 1;
            focusElevation = 1;
            VisibilityChanged?.Invoke(visibleElevation, maximumElevation);
            return;
        }

        maximumElevation = Mathf.Max(1, mapGenerator.MaximumElevation);
        visibleElevation = maximumElevation;
        focusElevation = visibleElevation;

        IsometricMapElevationElement[] foundElements = mapGenerator.GetComponentsInChildren<IsometricMapElevationElement>(true);
        for (int i = 0; i < foundElements.Length; i++)
        {
            if (foundElements[i] != null)
            {
                elevationElements.Add(foundElements[i]);
            }
        }

        IsometricDebrisOverlayController[] foundDebrisControllers = mapGenerator.GetComponentsInChildren<IsometricDebrisOverlayController>(true);
        for (int i = 0; i < foundDebrisControllers.Length; i++)
        {
            if (foundDebrisControllers[i] != null)
            {
                debrisOverlayControllers.Add(foundDebrisControllers[i]);
            }
        }

        IsometricFakeShadowOverlayController[] foundFakeShadowControllers = mapGenerator.GetComponentsInChildren<IsometricFakeShadowOverlayController>(true);
        for (int i = 0; i < foundFakeShadowControllers.Length; i++)
        {
            if (foundFakeShadowControllers[i] != null)
            {
                fakeShadowOverlayControllers.Add(foundFakeShadowControllers[i]);
            }
        }

        ApplyVisibility();
        VisibilityChanged?.Invoke(visibleElevation, maximumElevation);
    }

    private void ApplyVisibility()
    {
        for (int i = 0; i < elevationElements.Count; i++)
        {
            IsometricMapElevationElement element = elevationElements[i];
            if (element == null)
            {
                continue;
            }

            switch (element.ElementType)
            {
                case IsometricMapElevationElementType.SliceCap:
                    bool shouldShowSliceCap = element.Elevation == visibleElevation && visibleElevation < maximumElevation;
                    element.SetPresentation(shouldShowSliceCap ? sliceCapAlpha : 0f, false);
                    break;
                case IsometricMapElevationElementType.TopFace:
                    bool topFaceVisible = element.Elevation <= visibleElevation;
                    element.SetPresentation(topFaceVisible ? 1f : 0f, topFaceVisible);
                    break;
                case IsometricMapElevationElementType.TopOverlay:
                    bool topOverlayVisible = element.Elevation <= visibleElevation;
                    element.SetPresentation(topOverlayVisible ? 1f : 0f, false);
                    break;
                case IsometricMapElevationElementType.CutawaySideFace:
                    bool shouldShowCutawaySideFace = visibleElevation < maximumElevation && element.Elevation == visibleElevation;
                    element.SetPresentation(shouldShowCutawaySideFace ? 1f : 0f, false);
                    break;
                default:
                    bool layerVisible = element.Elevation <= visibleElevation;
                    element.SetPresentation(layerVisible ? 1f : 0f, false);
                    break;
            }
        }

        for (int i = 0; i < debrisOverlayControllers.Count; i++)
        {
            IsometricDebrisOverlayController controller = debrisOverlayControllers[i];
            if (controller != null)
            {
                controller.ApplyVisibleElevation(visibleElevation);
            }
        }

        for (int i = 0; i < fakeShadowOverlayControllers.Count; i++)
        {
            IsometricFakeShadowOverlayController controller = fakeShadowOverlayControllers[i];
            if (controller != null)
            {
                controller.ApplyVisibilityContext(visibleElevation, focusElevation);
            }
        }
    }

    private void SubscribeToMapGenerator()
    {
        if (mapGenerator == null)
        {
            return;
        }

        mapGenerator.MapGenerated -= HandleMapGenerated;
        mapGenerator.MapGenerated += HandleMapGenerated;
    }

    private void UnsubscribeFromMapGenerator()
    {
        if (mapGenerator == null)
        {
            return;
        }

        mapGenerator.MapGenerated -= HandleMapGenerated;
    }
}
