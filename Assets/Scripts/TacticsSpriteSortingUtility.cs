using UnityEngine;

public static class TacticsSpriteSortingUtility
{
    public static bool SortsInFrontOf(SpriteRenderer candidateForeground, SpriteRenderer candidateBackground)
    {
        if (candidateForeground == null || candidateBackground == null)
        {
            return false;
        }

        int foregroundLayerValue = SortingLayer.GetLayerValueFromID(candidateForeground.sortingLayerID);
        int backgroundLayerValue = SortingLayer.GetLayerValueFromID(candidateBackground.sortingLayerID);

        if (foregroundLayerValue != backgroundLayerValue)
        {
            return foregroundLayerValue > backgroundLayerValue;
        }

        return candidateForeground.sortingOrder > candidateBackground.sortingOrder;
    }
}
