using UnityEngine;

public readonly struct TacticsInventoryItemAddedEvent
{
    public TacticsInventoryItemAddedEvent(
        TacticsInventoryItemSaveData itemData,
        TacticsItemDefinition itemDefinition,
        int quantityAdded,
        bool mergedIntoExistingStack)
    {
        ItemData = itemData?.Clone();
        ItemDefinition = itemDefinition;
        QuantityAdded = Mathf.Max(1, quantityAdded);
        MergedIntoExistingStack = mergedIntoExistingStack;
    }

    public TacticsInventoryItemSaveData ItemData { get; }

    public TacticsItemDefinition ItemDefinition { get; }

    public int QuantityAdded { get; }

    public bool MergedIntoExistingStack { get; }

    public bool IsValid => ItemData != null && ItemDefinition != null;
}
