using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;

public static class TacticsUnityServicesBootstrap
{
    private const string ProfileArgumentName = "-ugsProfile";

    private static Task initializationTask;
    private static string activeProfile = string.Empty;

    public static string ActiveProfile => activeProfile;

    public static Task EnsureInitializedAsync()
    {
        initializationTask ??= InitializeInternalAsync();
        return initializationTask;
    }

    private static async Task InitializeInternalAsync()
    {
        activeProfile = ResolveProfileName();

        if (UnityServices.State == ServicesInitializationState.Uninitialized)
        {
            InitializationOptions options = new InitializationOptions();
            if (!string.IsNullOrWhiteSpace(activeProfile))
            {
                options.SetProfile(activeProfile);
            }

            await UnityServices.InitializeAsync(options);
        }

        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }
    }

    private static string ResolveProfileName()
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], ProfileArgumentName, StringComparison.OrdinalIgnoreCase))
            {
                return SanitizeProfileName(args[i + 1]);
            }
        }

#if UNITY_EDITOR
        return "editor";
#else
        return "player";
#endif
    }

    private static string SanitizeProfileName(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}
