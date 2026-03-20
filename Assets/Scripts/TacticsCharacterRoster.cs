using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "TacticsCharacterRoster", menuName = "Tactics/Characters/Character Roster")]
public sealed class TacticsCharacterRoster : ScriptableObject
{
    [SerializeField] private List<TacticsCharacterDefinition> playableCharacters = new();

    public IReadOnlyList<TacticsCharacterDefinition> PlayableCharacters => playableCharacters;
}
