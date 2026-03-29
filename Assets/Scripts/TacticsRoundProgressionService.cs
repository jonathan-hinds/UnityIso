using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class TacticsRoundProgressionService : MonoBehaviour
{
    [SerializeField] private TacticsRuntimeBootstrap runtimeBootstrap;
    [SerializeField] private ProceduralIsometricMapGenerator mapGenerator;
    [SerializeField] private TacticsCharacterRegistry characterRegistry;
    [SerializeField] private TacticsTurnManager turnManager;
    [SerializeField] private TacticsCoopSessionCoordinator coopSessionCoordinator;
    [SerializeField] private TacticsScreenFadeView screenFadeView;
    [SerializeField, Min(0.05f)] private float fadeDuration = 0.45f;

    public bool IsTransitionInProgress { get; private set; }

    public void AssignDependencies(
        TacticsRuntimeBootstrap bootstrap,
        ProceduralIsometricMapGenerator generator,
        TacticsCharacterRegistry registry,
        TacticsTurnManager manager,
        TacticsCoopSessionCoordinator coordinator,
        TacticsScreenFadeView fadeView)
    {
        runtimeBootstrap = bootstrap;
        mapGenerator = generator;
        characterRegistry = registry;
        turnManager = manager;
        coopSessionCoordinator = coordinator;
        screenFadeView = fadeView;
    }

    public bool CanDescend(TacticsCharacterController character)
    {
        return TryGetAvailableStairs(character, out _);
    }

    public bool TryGetAvailableStairs(TacticsCharacterController character, out TacticsStairsController stairs)
    {
        stairs = null;
        if (IsTransitionInProgress ||
            character == null ||
            !character.CanInteractThisTurn ||
            !AllEnemiesDefeated())
        {
            return false;
        }

        stairs = TacticsStairsController.FindBestAdjacentStairs(character);
        return stairs != null;
    }

    public bool RequestDescend(TacticsCharacterController character)
    {
        if (!TryGetAvailableStairs(character, out TacticsStairsController stairs))
        {
            return false;
        }

        if (coopSessionCoordinator != null && coopSessionCoordinator.IsOnlineSession)
        {
            return coopSessionCoordinator.RequestDescend(character, stairs);
        }

        TacticsMatchGenerationSettings nextSettings = runtimeBootstrap != null
            ? runtimeBootstrap.CreateNextRoundMatchSettings()
            : null;
        if (nextSettings == null)
        {
            return false;
        }

        return BeginRoundTransition(nextSettings, null, fadeDuration);
    }

    public TacticsCoopBattleSetup CreateNextRoundBattleSetup()
    {
        return runtimeBootstrap != null ? runtimeBootstrap.CreateNextRoundBattleSetup() : null;
    }

    public bool BeginRoundTransition(
        TacticsMatchGenerationSettings singlePlayerSettings,
        TacticsCoopBattleSetup coopBattleSetup,
        float requestedFadeDuration)
    {
        if (IsTransitionInProgress || runtimeBootstrap == null)
        {
            return false;
        }

        StartCoroutine(RoundTransitionRoutine(singlePlayerSettings, coopBattleSetup, requestedFadeDuration));
        return true;
    }

    private bool AllEnemiesDefeated()
    {
        characterRegistry ??= FindFirstObjectByType<TacticsCharacterRegistry>();
        return characterRegistry == null || !characterRegistry.HasLivingCharacters(TacticsUnitTeam.Enemy);
    }

    private IEnumerator RoundTransitionRoutine(
        TacticsMatchGenerationSettings singlePlayerSettings,
        TacticsCoopBattleSetup coopBattleSetup,
        float requestedFadeDuration)
    {
        IsTransitionInProgress = true;
        screenFadeView ??= FindFirstObjectByType<TacticsScreenFadeView>();

        if (screenFadeView != null)
        {
            yield return screenFadeView.FadeOut(requestedFadeDuration > 0f ? requestedFadeDuration : fadeDuration);
            screenFadeView.SetBlack();
        }

        yield return runtimeBootstrap.RestartBattleRoutine(singlePlayerSettings, coopBattleSetup);

        if (screenFadeView != null)
        {
            yield return screenFadeView.FadeIn(requestedFadeDuration > 0f ? requestedFadeDuration : fadeDuration);
        }

        IsTransitionInProgress = false;
    }
}
