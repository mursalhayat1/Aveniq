namespace Aveniq.Core;

/// <summary>
/// Optional startup and notification-area behavior. These are user preferences; runtime availability
/// (tray creation, registry access) is tracked separately so the UI never confuses a saved choice with
/// a feature that is actually active.
/// </summary>
public sealed class BackgroundSettings
{
    /// <summary>Master switch. When off, all optional background behavior is paused but preferences are kept.</summary>
    public bool Enabled { get; set; }

    /// <summary>Per-user Windows sign-in registration. Off by default.</summary>
    public bool LaunchAtSignIn { get; set; }

    /// <summary>Show the notification-area icon while background features are enabled.</summary>
    public bool ShowTrayIcon { get; set; } = true;

    /// <summary>Hide the main window instead of exiting when the user closes it.</summary>
    public bool CloseToTray { get; set; } = true;

    /// <summary>Allow occasional status notifications. Notifications are never required for core behavior.</summary>
    public bool Notifications { get; set; } = true;
}

/// <summary>The features that should be active after applying the master switch and individual preferences.</summary>
public readonly record struct BackgroundEffectiveState(
    bool AutoStart,
    bool TrayIcon,
    bool CloseToTray,
    bool Notifications)
{
    public static BackgroundEffectiveState Disabled => new(false, false, false, false);
}

public static class BackgroundFeaturePolicy
{
    /// <summary>
    /// Central precedence rule. A feature is effective only when the master switch and its own preference
    /// are enabled; close-to-tray additionally requires the tray preference.
    /// </summary>
    public static BackgroundEffectiveState Resolve(BackgroundSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var enabled = settings.Enabled;
        return new BackgroundEffectiveState(
            AutoStart: enabled && settings.LaunchAtSignIn,
            TrayIcon: enabled && settings.ShowTrayIcon,
            CloseToTray: enabled && settings.ShowTrayIcon && settings.CloseToTray,
            Notifications: enabled && settings.Notifications);
    }
}

public static class BackgroundLifecyclePolicy
{
    /// <summary>Only hide a window when close-to-tray is effective and a tray icon is actually available.</summary>
    public static bool ShouldHideOnClose(BackgroundEffectiveState effective, bool trayAvailable)
        => effective.CloseToTray && trayAvailable;

    /// <summary>
    /// Start hidden only for a Windows sign-in launch, when the tray can restore the app and setup is complete.
    /// A failed tray initialization must always leave a visible recovery path.
    /// </summary>
    public static bool ShouldStartHidden(
        bool startedByWindows,
        BackgroundEffectiveState effective,
        bool trayAvailable,
        bool firstRunCompleted)
        => startedByWindows && firstRunCompleted && effective.TrayIcon && trayAvailable;
}

/// <summary>Pure helpers shared by the registry writer, command-line startup detection and tests.</summary>
public static class BackgroundStartup
{
    public const string Argument = "--background";
    public const string RegistryValueName = "Aveniq";
    public const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    /// <summary>Build a quoted Run command so installed paths containing spaces work correctly.</summary>
    public static string BuildCommand(string executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
            throw new ArgumentException("The executable path is required.", nameof(executablePath));

        var fullPath = Path.GetFullPath(executablePath.Trim());
        if (fullPath.Contains('"'))
            throw new ArgumentException("The executable path contains an invalid quote.", nameof(executablePath));

        return $"\"{fullPath}\" {Argument}";
    }

    public static bool IsBackgroundLaunch(IEnumerable<string> args)
        => args.Any(a => string.Equals(a, Argument, StringComparison.OrdinalIgnoreCase));
}
