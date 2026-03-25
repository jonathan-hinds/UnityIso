using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-200)]
public sealed class TacticsCharacterRegistry : MonoBehaviour
{
    private readonly List<TacticsCharacterController> characters = new();
    private readonly Dictionary<string, TacticsCharacterController> charactersByRuntimeId = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<Vector2Int, TacticsCharacterController> charactersByTile = new();
    private readonly Dictionary<TacticsCharacterController, string> runtimeIdsByCharacter = new();
    private readonly Dictionary<TacticsCharacterController, Vector2Int> tilesByCharacter = new();

    public static TacticsCharacterRegistry Instance => FindFirstObjectByType<TacticsCharacterRegistry>();
    public int CharacterCount => characters.Count;

    public void Register(TacticsCharacterController character)
    {
        if (character == null)
        {
            return;
        }

        if (!characters.Contains(character))
        {
            characters.Add(character);
        }

        Refresh(character);
    }

    public void Unregister(TacticsCharacterController character)
    {
        if (character == null)
        {
            return;
        }

        characters.Remove(character);
        RemoveCharacterIndexes(character);
    }

    public void Refresh(TacticsCharacterController character)
    {
        if (character == null)
        {
            return;
        }

        if (!characters.Contains(character))
        {
            characters.Add(character);
        }

        RemoveCharacterIndexes(character);

        string runtimeCharacterId = character.RuntimeCharacterId;
        if (!string.IsNullOrWhiteSpace(runtimeCharacterId))
        {
            charactersByRuntimeId[runtimeCharacterId] = character;
            runtimeIdsByCharacter[character] = runtimeCharacterId;
        }

        if (CanIndexTile(character))
        {
            charactersByTile[character.GridPosition] = character;
            tilesByCharacter[character] = character.GridPosition;
        }
    }

    public void GetAllCharacters(List<TacticsCharacterController> results, bool includeInactive = false)
    {
        if (results == null)
        {
            return;
        }

        results.Clear();
        for (int i = characters.Count - 1; i >= 0; i--)
        {
            TacticsCharacterController candidate = characters[i];
            if (candidate == null)
            {
                RemoveAt(i);
                continue;
            }

            if (!includeInactive && !IsCharacterQueryable(candidate))
            {
                continue;
            }

            results.Add(candidate);
        }
    }

    public void GetHostileCharacters(TacticsCharacterController source, List<TacticsCharacterController> results)
    {
        if (results == null)
        {
            return;
        }

        results.Clear();
        if (source == null)
        {
            return;
        }

        for (int i = characters.Count - 1; i >= 0; i--)
        {
            TacticsCharacterController candidate = characters[i];
            if (candidate == null)
            {
                RemoveAt(i);
                continue;
            }

            if (!IsCharacterQueryable(candidate) ||
                ReferenceEquals(candidate, source) ||
                candidate.Team == source.Team)
            {
                continue;
            }

            results.Add(candidate);
        }
    }

    public void GetAlliedCharacters(TacticsCharacterController source, bool includeSelf, List<TacticsCharacterController> results)
    {
        if (results == null)
        {
            return;
        }

        results.Clear();
        if (source == null)
        {
            return;
        }

        for (int i = characters.Count - 1; i >= 0; i--)
        {
            TacticsCharacterController candidate = characters[i];
            if (candidate == null)
            {
                RemoveAt(i);
                continue;
            }

            if (!IsCharacterQueryable(candidate) || candidate.Team != source.Team)
            {
                continue;
            }

            if (!includeSelf && ReferenceEquals(candidate, source))
            {
                continue;
            }

            results.Add(candidate);
        }
    }

    public bool TryGetCharacterByRuntimeId(string runtimeCharacterId, out TacticsCharacterController character)
    {
        character = null;
        if (string.IsNullOrWhiteSpace(runtimeCharacterId))
        {
            return false;
        }

        if (!charactersByRuntimeId.TryGetValue(runtimeCharacterId, out character) || character == null)
        {
            return false;
        }

        if (!IsCharacterQueryable(character))
        {
            return false;
        }

        return true;
    }

    public bool TryGetCharacterAtTile(Vector2Int tile, out TacticsCharacterController character, TacticsCharacterController exclude = null)
    {
        character = null;
        if (!charactersByTile.TryGetValue(tile, out TacticsCharacterController candidate) || candidate == null)
        {
            return false;
        }

        if (ReferenceEquals(candidate, exclude) || !IsCharacterQueryable(candidate))
        {
            return false;
        }

        character = candidate;
        return true;
    }

    private bool CanIndexTile(TacticsCharacterController character)
    {
        return IsCharacterQueryable(character);
    }

    private bool IsCharacterQueryable(TacticsCharacterController character)
    {
        return character != null &&
               character.isActiveAndEnabled &&
               character.IsAlive;
    }

    private void RemoveAt(int index)
    {
        if (index < 0 || index >= characters.Count)
        {
            return;
        }

        TacticsCharacterController character = characters[index];
        characters.RemoveAt(index);
        RemoveCharacterIndexes(character);
    }

    private void RemoveCharacterIndexes(TacticsCharacterController character)
    {
        if (character == null)
        {
            return;
        }

        if (runtimeIdsByCharacter.TryGetValue(character, out string previousRuntimeId))
        {
            if (!string.IsNullOrWhiteSpace(previousRuntimeId) &&
                charactersByRuntimeId.TryGetValue(previousRuntimeId, out TacticsCharacterController indexedCharacter) &&
                ReferenceEquals(indexedCharacter, character))
            {
                charactersByRuntimeId.Remove(previousRuntimeId);
            }

            runtimeIdsByCharacter.Remove(character);
        }

        if (tilesByCharacter.TryGetValue(character, out Vector2Int previousTile))
        {
            if (charactersByTile.TryGetValue(previousTile, out TacticsCharacterController indexedCharacter) &&
                ReferenceEquals(indexedCharacter, character))
            {
                charactersByTile.Remove(previousTile);
            }

            tilesByCharacter.Remove(character);
        }
    }
}
