using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class IsometricMapLayerVisibilityController : MonoBehaviour
{
    [SerializeField] private ProceduralIsometricMapGenerator mapGenerator;
    [SerializeField, Range(0f, 1f)] private float obscuredLayerAlpha = 0.12f;
    [SerializeField, Range(0f, 1f)] private float sliceCapAlpha = 0.32f;

    private readonly List<IsometricMapElevationElement> elevationElements = new();
    private int maximumElevation = 1;
    private int visibleElevation = 0;

    public event Action<int, int> VisibilityChanged;

    public int MaximumElevation => maximumElevation;
    public int VisibleElevation => visibleElevation;

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

    private void HandleMapGenerated()
    {
        RefreshFromMap();
    }

    private void RefreshFromMap()
    {
        elevationElements.Clear();

        if (mapGenerator == null)
        {
            maximumElevation = 1;
            visibleElevation = 1;
            VisibilityChanged?.Invoke(visibleElevation, maximumElevation);
            return;
        }

        maximumElevation = Mathf.Max(1, mapGenerator.MaximumElevation);
        visibleElevation = Mathf.Clamp(visibleElevation <= 0 ? maximumElevation : visibleElevation, 1, maximumElevation);

        IsometricMapElevationElement[] foundElements = mapGenerator.GetComponentsInChildren<IsometricMapElevationElement>(true);
        for (int i = 0; i < foundElements.Length; i++)
        {
            if (foundElements[i] != null)
            {
                elevationElements.Add(foundElements[i]);
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
                    element.SetPresentation(topFaceVisible ? 1f : obscuredLayerAlpha, topFaceVisible);
                    break;
                default:
                    bool layerVisible = element.Elevation <= visibleElevation;
                    element.SetPresentation(layerVisible ? 1f : obscuredLayerAlpha, false);
                    break;
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
