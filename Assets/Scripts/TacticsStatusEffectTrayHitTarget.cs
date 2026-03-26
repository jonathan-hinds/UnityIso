using UnityEngine;

[DisallowMultipleComponent]
public sealed class TacticsStatusEffectTrayHitTarget : MonoBehaviour
{
    public TacticsCharacterController Character { get; private set; }

    public void Bind(TacticsCharacterController character)
    {
        Character = character;
    }
}
