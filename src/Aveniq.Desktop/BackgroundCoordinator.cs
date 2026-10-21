using Aveniq.App;
using Aveniq.Core;
using Microsoft.Extensions.Logging;

namespace Aveniq.Desktop;

public readonly record struct BackgroundApplyResult(
    BackgroundEffectiveState Effective,
    bool AutoStartActive,
    bool TrayAvailable,
    IReadOnlyList<string> Errors);

/// <summary>
/// Owns the optional desktop behavior and is the single place that turns saved preferences into
/// effective behavior. It never touches mounts, rclone, caches or user data.
/// </summary>
public sealed class BackgroundCoordinator : IDisposable
{
    private readonly AppHost _host;
    private readonly MainWindow _window;
    private readonly Action _showWindow;
    private readonly ILogger _log;
    private readonly StartupRegistrationService _startup;
    private readonly TrayIconService _tray;
    private readonly HashSet<string> _notified = new(StringComparer.Ordinal);
    private bool _hiddenToTray;
    private bool _disposed;
    private bool _autoStartActive;
    private string? _startupError;
    private string? _trayError;

    public BackgroundCoordinator(
        AppHost host,
        MainWindow window,
        Action showWindow,
        Action showSettings,
        Action requestExit)
    {
        _host = host;
        _window = window;
        _showWindow = showWindow;
        _log = host.LoggerFactory.CreateLogger<BackgroundCoordinator>();
        _startup = new StartupRegistrationService(host.LoggerFactory.CreateLogger<StartupRegistrationService>());
        _tray = new TrayIconService(host.LoggerFactory.CreateLogger<TrayIconService>(), showWindow, showSettings, requestExit);
        ExecutablePath = ResolveExecutablePath();
    }

    public event Action? StateChanged;

    public BackgroundEffectiveState Effective => BackgroundFeaturePolicy.Resolve(_host.Settings.Background);
    public bool TrayAvailable => _tray.Available;
    public bool AutoStartActive => _autoStartActive;
    public bool IsHiddenToTray => _hiddenToTray;
    public string? StartupError => _startupError;
    public string? TrayError => _trayError;
    public string ExecutablePath { get; }

    public string? LastError
    {
        get
        {
            var errors = new List<string>();
            if (!string.IsNullOrWhiteSpace(_startupError)) errors.Add(_startupError);
            if (!string.IsNullOrWhiteSpace(_trayError)) errors.Add(_trayError);
            return errors.Count == 0 ? null : string.Join(" ", errors);
        }
    }

    /// <summary>Reconcile Windows registration and create the tray icon when the saved state requires it.</summary>
    public void Initialize() => Apply("startup");

    public BackgroundApplyResult Apply(string reason = "settings")
    {
        if (_disposed)
            return new BackgroundApplyResult(BackgroundEffectiveState.Disabled, false, false, ["Aveniq is shutting down."]);

        var errors = new List<string>();
        var effective = Effective;

        // The startup value is always reconciled, including removing a stale value while master is off.
        var startup = effective.AutoStart ? _startup.Register(ExecutablePath) : _startup.Unregister();
        _autoStartActive = startup.IsRegistered;
        _startupError = startup.Success ? null : startup.Error;
        if (!startup.Success && startup.Error is { } startupError) errors.Add(startupError);

        if (effective.TrayIcon)
        {
            if (_tray.Initialize())
            {
                _trayError = null;
            }
            else
            {
                _trayError = _tray.Error ?? "The notification-area icon could not be created.";
                errors.Add(_trayError);
            }
        }
        else
        {
            if (_tray.Available) _log.LogInformation("Tray icon disabled by background settings");
            try
            {
                _tray.Dispose();
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Tray icon disposal failed while disabling background settings");
            }

            _trayError = null;
        }

        // Never leave the user with a hidden, inaccessible window after the tray is disabled or fails.
        if (_hiddenToTray && !_tray.Available)
        {
            _hiddenToTray = false;
            _log.LogWarning("Tray is unavailable while the window is hidden; restoring the main window");
            _showWindow();
        }

        if (errors.Count == 0)
        {
            _log.LogInformation(
                "Background settings applied ({Reason}): master={Master}, auto-start={AutoStart}, tray={Tray}, close-to-tray={CloseToTray}, notifications={Notifications}",
                reason, _host.Settings.Background.Enabled, _autoStartActive, _tray.Available,
                effective.CloseToTray && _tray.Available, effective.Notifications && _tray.Available);
        }
        else
        {
            _log.LogWarning("Background settings applied with {Count} issue(s) ({Reason}): {Errors}",
                errors.Count, reason, string.Join(" ", errors));
        }

        NotifyStateChanged();
        return new BackgroundApplyResult(effective, _autoStartActive, _tray.Available, errors);
    }

    public bool ShouldHideOnClose()
        => BackgroundLifecyclePolicy.ShouldHideOnClose(Effective, TrayAvailable);

    public bool ShouldStartHidden(bool startedByWindows, bool firstRunCompleted)
        => BackgroundLifecyclePolicy.ShouldStartHidden(startedByWindows, Effective, TrayAvailable, firstRunCompleted);

    public bool HideToTray()
    {
        if (_disposed || !_window.IsVisible || !ShouldHideOnClose()) return false;
        _hiddenToTray = true;
        _window.Hide();
        _log.LogInformation("Main window hidden to tray");
        NotifyOnce("background", "Aveniq is still running", "Use the tray icon to open Aveniq. Cloud drives stay available.");
        return true;
    }

    public void MarkStartedHidden()
    {
        if (!_tray.Available)
        {
            _log.LogWarning("Tray became unavailable during background startup; restoring the main window");
            _showWindow();
            return;
        }

        _hiddenToTray = true;
        NotifyOnce("background", "Aveniq started in the background", "Use the tray icon to open Aveniq.");
    }

    public void OnWindowRestored()
    {
        if (!_hiddenToTray) return;
        _hiddenToTray = false;
        _log.LogInformation("Main window restored from the tray");
    }

    /// <summary>At most one balloon per key per session; failures are logged, never fatal.</summary>
    public void NotifyOnce(string key, string title, string message)
    {
        if (_disposed || !Effective.Notifications) return;
        if (!_notified.Add(key)) return;
        if (!_tray.ShowNotification(title, message))
            _log.LogWarning("Background notification could not be shown: {Error}", _tray.Error);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try
        {
            _tray.Dispose();
            _log.LogInformation("Tray resources disposed");
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Tray resources could not be disposed cleanly");
        }

        StateChanged = null;
    }

    private void NotifyStateChanged()
    {
        try
        {
            StateChanged?.Invoke();
        }
        catch (Exception ex)
        {
            // A settings view must never be able to break background reconciliation.
            _log.LogWarning(ex, "Background state change notification failed");
        }
    }

    private static string ResolveExecutablePath()
    {
        var process = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(process) &&
            string.Equals(Path.GetFileName(process), "Aveniq.exe", StringComparison.OrdinalIgnoreCase))
            return process;

        // A framework-dependent launch can report dotnet.exe. Register the apphost produced by the
        // desktop project instead of accidentally registering the .NET host without arguments.
        return Path.Combine(AppContext.BaseDirectory, "Aveniq.exe");
    }
}
