using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

public interface ITacticsAccountSessionService
{
    event Action StateChanged;

    bool IsInitialized { get; }
    bool IsBusy { get; }
    bool IsSignedIn { get; }
    string PlayerId { get; }
    string Username { get; }
    string StatusMessage { get; }
    string ErrorMessage { get; }
    TacticsCloudSavePlayerProfile Profile { get; }

    Task InitializeAsync();
    Task<bool> SignInAsync(string username, string password);
    Task<bool> RegisterAsync(string username, string password);
    void SignOut();
    Task FlushAsync();
}

public sealed class TacticsAccountSessionService : ITacticsAccountSessionService
{
    private const string LastUsernameKey = "tactics.account.last-username";

    private readonly object stateLock = new object();
    private TacticsCloudSavePlayerProfile profile;
    private bool isInitialized;
    private bool isBusy;
    private string username = string.Empty;
    private string statusMessage = "Sign in to access online co-op progression.";
    private string errorMessage = string.Empty;

    public event Action StateChanged;

    public bool IsInitialized => isInitialized;
    public bool IsBusy => isBusy;
    public bool IsSignedIn => IsAuthenticationAvailable() && AuthenticationService.Instance.IsSignedIn;
    public string PlayerId => IsAuthenticationAvailable() && AuthenticationService.Instance.IsSignedIn
        ? AuthenticationService.Instance.PlayerId
        : string.Empty;
    public string Username => username;
    public string StatusMessage => statusMessage;
    public string ErrorMessage => errorMessage;
    public TacticsCloudSavePlayerProfile Profile => profile;

    public async Task InitializeAsync()
    {
        if (isInitialized)
        {
            return;
        }

        await SetBusyStateAsync("Connecting account services...", async () =>
        {
            await TacticsUnityServicesBootstrap.EnsureInitializedAsync();

            string cachedUsername = PlayerPrefs.GetString(LastUsernameKey, string.Empty);
            if (IsAuthenticationAvailable() && AuthenticationService.Instance.SessionTokenExists)
            {
                try
                {
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                    await LoadProfileAsync(cachedUsername);
                    statusMessage = $"Signed in as {username}.";
                    errorMessage = string.Empty;
                }
                catch (Exception exception)
                {
                    Debug.LogWarning($"Failed to restore cached account session: {exception.Message}");
                    AuthenticationService.Instance.SignOut();
                    profile = null;
                    username = string.Empty;
                    statusMessage = "Sign in to access online co-op progression.";
                    errorMessage = string.Empty;
                }
            }
            else
            {
                profile = null;
                username = string.Empty;
                statusMessage = "Sign in to access online co-op progression.";
                errorMessage = string.Empty;
            }

            isInitialized = true;
        });
    }

    public async Task<bool> SignInAsync(string username, string password)
    {
        return await AuthenticateAsync(
            "Signing in...",
            username,
            password,
            (value, secret) => AuthenticationService.Instance.SignInWithUsernamePasswordAsync(value, secret),
            "Signed in successfully.");
    }

    public async Task<bool> RegisterAsync(string username, string password)
    {
        return await AuthenticateAsync(
            "Creating account...",
            username,
            password,
            (value, secret) => AuthenticationService.Instance.SignUpWithUsernamePasswordAsync(value, secret),
            "Account created and signed in.");
    }

    public void SignOut()
    {
        if (IsAuthenticationAvailable() && AuthenticationService.Instance.IsSignedIn)
        {
            AuthenticationService.Instance.SignOut();
        }

        profile = null;
        statusMessage = "Signed out. Sign in to access online co-op progression.";
        errorMessage = string.Empty;
        NotifyStateChanged();
    }

    public async Task FlushAsync()
    {
        if (profile != null)
        {
            await profile.FlushAsync();
        }
    }

    private async Task<bool> AuthenticateAsync(
        string busyMessage,
        string candidateUsername,
        string password,
        Func<string, string, Task> operation,
        string successMessage)
    {
        string sanitizedUsername = SanitizeUsername(candidateUsername);
        if (!ValidateCredentials(sanitizedUsername, password, out string validationMessage))
        {
            errorMessage = validationMessage;
            statusMessage = "Fix the highlighted account details and try again.";
            NotifyStateChanged();
            return false;
        }

        return await SetBusyStateAsync(busyMessage, async () =>
        {
            await TacticsUnityServicesBootstrap.EnsureInitializedAsync();
            if (AuthenticationService.Instance.IsSignedIn)
            {
                AuthenticationService.Instance.SignOut();
            }

            try
            {
                await operation(sanitizedUsername, password);
                SaveLastUsername(sanitizedUsername);
                await LoadProfileAsync(sanitizedUsername);
                statusMessage = successMessage;
                errorMessage = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                profile = null;
                statusMessage = "Account action failed.";
                errorMessage = FormatAuthException(exception);
                Debug.LogWarning($"Authentication action failed: {exception}");
                return false;
            }
        });
    }

    private async Task LoadProfileAsync(string preferredUsername)
    {
        profile = new TacticsCloudSavePlayerProfile(PlayerId, preferredUsername);
        await profile.InitializeAsync();
        username = string.IsNullOrWhiteSpace(profile.Username)
            ? preferredUsername
            : profile.Username;
        if (!string.IsNullOrWhiteSpace(username))
        {
            SaveLastUsername(username);
        }
    }

    private static string SanitizeUsername(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static bool ValidateCredentials(string username, string password, out string message)
    {
        if (username.Length < 3 || username.Length > 20)
        {
            message = "Username must be 3-20 characters.";
            return false;
        }

        if (password == null || password.Length < 8)
        {
            message = "Password must be at least 8 characters.";
            return false;
        }

        message = string.Empty;
        return true;
    }

    private static string FormatAuthException(Exception exception)
    {
        if (exception == null)
        {
            return "Unknown authentication error.";
        }

        return string.IsNullOrWhiteSpace(exception.Message)
            ? "Authentication request failed."
            : exception.Message;
    }

    private void SaveLastUsername(string value)
    {
        PlayerPrefs.SetString(LastUsernameKey, value ?? string.Empty);
        PlayerPrefs.Save();
    }

    private static bool IsAuthenticationAvailable()
    {
        return UnityServices.State != ServicesInitializationState.Uninitialized;
    }

    private async Task SetBusyStateAsync(string busyMessage, Func<Task> work)
    {
        await SetBusyStateAsync<bool>(busyMessage, async () =>
        {
            await work();
            return true;
        });
    }

    private async Task<T> SetBusyStateAsync<T>(string busyMessage, Func<Task<T>> work)
    {
        lock (stateLock)
        {
            isBusy = true;
            statusMessage = busyMessage;
            errorMessage = string.Empty;
        }

        NotifyStateChanged();

        try
        {
            return await work();
        }
        finally
        {
            lock (stateLock)
            {
                isBusy = false;
            }

            NotifyStateChanged();
        }
    }

    private void NotifyStateChanged()
    {
        StateChanged?.Invoke();
    }
}
