using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class TacticsForegroundOccluderGroup : MonoBehaviour
{
    [SerializeField] private List<SpriteRenderer> occluders = new List<SpriteRenderer>();

    public IReadOnlyList<SpriteRenderer> Occluders => occluders;

    private void OnEnable()
    {
        TacticsForegroundOccluderRegistry.Register(this);
    }

    private void OnDisable()
    {
        TacticsForegroundOccluderRegistry.Unregister(this);
    }

    public void RegisterOccluder(SpriteRenderer occluder)
    {
        if (occluder == null || occluders.Contains(occluder))
        {
            return;
        }

        occluders.Add(occluder);
    }

    [ContextMenu("Rebuild From Children")]
    public void RebuildFromChildren()
    {
        occluders.Clear();

        SpriteRenderer[] childRenderers = GetComponentsInChildren<SpriteRenderer>(includeInactive: true);
        for (int i = 0; i < childRenderers.Length; i++)
        {
            RegisterOccluder(childRenderers[i]);
        }
    }
}
