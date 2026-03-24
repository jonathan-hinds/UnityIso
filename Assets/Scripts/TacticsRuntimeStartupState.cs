using UnityEngine;

public static class TacticsRuntimeStartupState
{
    public static bool GameplayStartRequested { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        GameplayStartRequested = false;
    }

    public static void RequestGameplayStart()
    {
        GameplayStartRequested = true;
    }

    public static void ResetGameplayStart()
    {
        GameplayStartRequested = false;
    }
}
