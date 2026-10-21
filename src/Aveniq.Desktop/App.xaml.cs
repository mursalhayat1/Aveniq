using System.ComponentModel;
using System.Threading;
using System.Windows;
using Aveniq.App;
using Aveniq.Core;
using Aveniq.Desktop.ViewModels;
using Aveniq.Desktop.Views;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace Aveniq.Desktop;

public partial class App : Application
{
    private const string SingleInstanceName = @"Local\Aveniq.Desktop.SingleInstance";
    private const string ActivationEventName = @"Local\Aveniq.Desktop.Activate";

    private Mutex? _single;
    private EventWaitHandle? _activationEvent;
    private ManualResetEvent? _activationStop;
    private Thread? _activationThread;
    private AppHost? _host;
    private BackgroundCoordinator? _background;
    private bool _shuttingDown;
    private bool _exitRequested;
    private volatile bool _activationPending;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        WindowTheme.EnableForAllWindows();

        _single = new Mutex(true, SingleInstanceName, out var created);
        if (!created)
        {
            SignalRunningInstance();
            Shutdown();
            return;
        }

        StartActivationWatcher();

        _host = AppHost.Create();
        if (!_host.TryAcquireInstanceLock())
        {
            MessageBox.Show("Another Aveniq process (for example the command-line tool) is using the data folder.", "Aveniq");
            Shutdown();
            return;
        }

        DispatcherUnhandledException += (_, args) =>
        {
            _host.LoggerFactory.CreateLogger("App").LogError(args.Exception, "Unhandled UI exception");
            MessageBox.Show(Diagnostics.Redactor.Redact(args.Exception.Message), "Aveniq error", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };
        SystemEvents.PowerModeChanged += (_, args) =>
        {
            if (!_shuttingDown && args.Mode == PowerModes.Resume && _host != null)
                _ = _host.Mounts.OnSystemResumeAsync();
        };

        var window = new MainWindow();
        MainWindow = window;
        window.Closing += OnClosing;

        _background = new BackgroundCoordinator(_host, window, ShowMainWindow, ShowSettings, RequestExit);
        var vm = new MainViewModel(_host, _background);
        window.DataContext = vm;

        var startedByWindows = BackgroundStartup.IsBackgroundLaunch(e.Args);
        _background.Initialize();
        var startHidden = _background.ShouldStartHidden(startedByWindows, _host.Settings.FirstRunCompleted);
        if (!startHidden) window.Show();

        try
        {
            await _host.InitializeAsync();
        }
        catch (Exception ex)
        {
            startHidden = false;
            if (!window.IsVisible) window.Show();
            MessageBox.Show(window, "Startup problem: " + Diagnostics.Redactor.Redact(ex.Message), "Aveniq", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        if (!_host.Settings.FirstRunCompleted)
        {
            startHidden = false;
            if (!window.IsVisible) window.Show();
            var first = new FirstRunWindow(_host) { Owner = window };
            first.ShowDialog();
        }

        if (_activationPending)
        {
            _activationPending = false;
            startHidden = false;
            ShowMainWindow();
        }

        if (startHidden && !window.IsVisible) _background.MarkStartedHidden();
        vm.Selected.OnShown();
    }

    private void StartActivationWatcher()
    {
        _activationEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ActivationEventName, out _);
        _activationStop = new ManualResetEvent(false);
        _activationThread = new Thread(() =>
        {
            var handles = new WaitHandle[] { _activationEvent, _activationStop };
            while (true)
            {
                try
                {
                    var index = WaitHandle.WaitAny(handles);
                    if (index == 1) return;
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (_shuttingDown) return;
                        if (MainWindow is null) _activationPending = true;
                        else ShowMainWindow();
                    }));
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                catch (Exception)
                {
                    return;
                }
            }
        })
        {
            IsBackground = true,
            Name = "Aveniq activation listener"
        };
        _activationThread.Start();
    }

    private static void SignalRunningInstance()
    {
        try
        {
            if (EventWaitHandle.TryOpenExisting(ActivationEventName, out var activation))
            {
                using (activation) activation.Set();
                return;
            }
        }
        catch (Exception)
        {
            // Fall through to the visible message below; the first instance may still be starting.
        }

        MessageBox.Show("Aveniq is already running.", "Aveniq", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ShowMainWindow()
    {
        if (_shuttingDown || MainWindow is not { } window) return;

        if (!window.IsVisible) window.Show();
        if (window.WindowState == WindowState.Minimized) window.WindowState = WindowState.Normal;
        window.Activate();
        window.Topmost = true;
        window.Topmost = false;
        window.Focus();
        _background?.OnWindowRestored();
    }

    private void ShowSettings()
    {
        ShowMainWindow();
        if (MainWindow?.DataContext is MainViewModel vm) vm.Navigate("Settings");
    }

    private void RequestExit()
    {
        if (_shuttingDown) return;
        _exitRequested = true;
        if (MainWindow is { } window)
        {
            if (!window.IsVisible) ShowMainWindow();
            window.Close();
        }
        else
        {
            Shutdown();
        }
    }

    private async void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_shuttingDown || _host is null) return;

        // A normal close hides only when the tray is actually available; explicit Exit and every
        // failure path below use the existing orderly shutdown.
        if (!_exitRequested && _background?.ShouldHideOnClose() == true)
        {
            e.Cancel = true;
            _background.HideToTray();
            return;
        }

        e.Cancel = true;
        var keep = _host.Settings.KeepMountsAfterExit;
        if (_host.Mounts.Sessions.Count > 0)
        {
            var running = _host.CurrentLaunch is { State: Games.LaunchState.Running };
            var answer = MessageBox.Show(MainWindow!,
                (running ? "A game is still running.\n\n" : "") +
                "Keep the cloud drives mounted after closing Aveniq?\n\nYes: drives stay available and are re-attached next time.\nNo: drives are unmounted (pending uploads get up to 60 s to finish).",
                "Close Aveniq", MessageBoxButton.YesNoCancel, MessageBoxImage.Question, keep || running ? MessageBoxResult.Yes : MessageBoxResult.No);
            if (answer == MessageBoxResult.Cancel)
            {
                _exitRequested = false;
                return;
            }
            keep = answer == MessageBoxResult.Yes;
        }

        _shuttingDown = true;
        var log = _host.LoggerFactory.CreateLogger("App");
        log.LogInformation(_exitRequested ? "Explicit exit requested from the tray" : "Exit requested from the main window");
        MainWindow!.IsEnabled = false;
        MainWindow.Title = "Aveniq — closing…";

        _background?.Dispose();
        try
        {
            await _host.ShutdownAsync(keep);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Orderly shutdown failed");
        }

        try
        {
            await _host.DisposeAsync();
        }
        catch (Exception ex)
        {
            try { log.LogError(ex, "Application resource disposal failed"); } catch (Exception) { }
        }

        Shutdown();
    }

    private void ReleaseSingleInstance()
    {
        try
        {
            _single?.ReleaseMutex();
        }
        catch (ApplicationException)
        {
            // The mutex was already released; process shutdown still closes the handle.
        }

        _single?.Dispose();
        _single = null;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _activationStop?.Set();
        try { _activationThread?.Join(500); } catch (Exception) { }
        _activationEvent?.Dispose();
        _activationEvent = null;
        _activationStop?.Dispose();
        _activationStop = null;
        ReleaseSingleInstance();
        base.OnExit(e);
    }
}
