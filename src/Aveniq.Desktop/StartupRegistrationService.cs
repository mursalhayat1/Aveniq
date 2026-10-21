using Aveniq.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace Aveniq.Desktop;

public readonly record struct StartupRegistrationResult(bool Success, bool IsRegistered, string? Error);

/// <summary>
/// Per-user sign-in registration. The registry is only touched for the optional startup feature; mounted
/// drives, rclone processes and user data are never involved.
/// </summary>
public sealed class StartupRegistrationService
{
    private readonly ILogger _log;

    public StartupRegistrationService(ILogger<StartupRegistrationService> log) => _log = log;

    public StartupRegistrationResult Register(string executablePath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
            {
                var error = "Aveniq could not find its executable. Install Aveniq again and retry.";
                _log.LogWarning("Startup registration failed: executable not found at {Path}", executablePath);
                return new StartupRegistrationResult(false, IsRegistered(), error);
            }

            var command = BackgroundStartup.BuildCommand(executablePath);
            using var key = Registry.CurrentUser.CreateSubKey(BackgroundStartup.RunKeyPath, writable: true);
            if (key is null)
                return new StartupRegistrationResult(false, IsRegistered(), "Windows could not open the current user's startup settings.");

            if (key.GetValue(BackgroundStartup.RegistryValueName) is string existing &&
                string.Equals(existing, command, StringComparison.Ordinal))
            {
                _log.LogDebug("Startup registration already current for {Path}", executablePath);
                return new StartupRegistrationResult(true, true, null);
            }

            key.SetValue(BackgroundStartup.RegistryValueName, command, RegistryValueKind.String);
            var written = key.GetValue(BackgroundStartup.RegistryValueName) as string;
            if (!string.Equals(written, command, StringComparison.Ordinal))
            {
                _log.LogWarning("Startup registration verification failed for {Path}", executablePath);
                return new StartupRegistrationResult(false, IsRegistered(), "Windows did not keep the startup registration. Try again.");
            }

            _log.LogInformation("Startup registration enabled for the current Windows user");
            return new StartupRegistrationResult(true, true, null);
        }
        catch (Exception ex)
        {
            var error = FriendlyError(ex, "enable");
            _log.LogWarning("Startup registration failed: {Error}", error);
            return new StartupRegistrationResult(false, IsRegistered(), error);
        }
    }

    public StartupRegistrationResult Unregister()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(BackgroundStartup.RunKeyPath, writable: true);
            if (key is null)
                return new StartupRegistrationResult(true, false, null);

            var hadValue = key.GetValue(BackgroundStartup.RegistryValueName) is not null;
            key.DeleteValue(BackgroundStartup.RegistryValueName, throwOnMissingValue: false);
            var stillThere = key.GetValue(BackgroundStartup.RegistryValueName) is string value && !string.IsNullOrWhiteSpace(value);
            if (stillThere)
            {
                _log.LogWarning("Startup registration removal verification failed");
                return new StartupRegistrationResult(false, true, "Windows did not remove the startup registration. Try again.");
            }

            if (hadValue) _log.LogInformation("Startup registration removed for the current Windows user");
            return new StartupRegistrationResult(true, false, null);
        }
        catch (Exception ex)
        {
            var error = FriendlyError(ex, "remove");
            _log.LogWarning("Startup registration removal failed: {Error}", error);
            return new StartupRegistrationResult(false, IsRegistered(), error);
        }
    }

    /// <summary>Actual registry state, used for status after a failed write or an external change.</summary>
    public bool IsRegistered()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(BackgroundStartup.RunKeyPath);
            return key?.GetValue(BackgroundStartup.RegistryValueName) is string value && !string.IsNullOrWhiteSpace(value);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static string FriendlyError(Exception ex, string action)
    {
        if (ex is UnauthorizedAccessException or System.Security.SecurityException)
            return $"Windows denied access while trying to {action} the startup entry.";
        if (ex is IOException)
            return $"Windows could not {action} the startup entry because the registry is unavailable.";
        return $"Windows could not {action} the startup entry: {ex.Message}";
    }
}
