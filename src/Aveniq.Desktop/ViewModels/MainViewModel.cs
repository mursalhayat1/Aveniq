using System.Collections.ObjectModel;
using Aveniq.App;
using Aveniq.Core;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Aveniq.Desktop.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    public MainViewModel(AppHost host, BackgroundCoordinator? background = null)
    {
        Host = host;
        Pages = new ObservableCollection<PageViewModel>
        {
            new DashboardViewModel(host, Navigate),
            new CloudDrivesViewModel(host),
            new GameLibraryViewModel(host),
            new AcceleratorViewModel(host),
            new SmartCacheViewModel(host),
            new PerformanceViewModel(host),
            new SettingsViewModel(host, background),
            new DiagnosticsViewModel(host),
        };
        PrimaryPages = Pages.Take(6).ToArray();
        UtilityPages = Pages.Skip(6).ToArray();
        _selected = Pages[0];
        _primarySelected = Pages[0];
        host.StateChanged += () => Ui(() =>
        {
            Selected.OnStateChanged();
            UpdateStatus(host.Metrics.Latest);
        });
        host.Metrics.Updated += m => Ui(() =>
        {
            Selected.OnMetrics(m);
            UpdateStatus(m);
        });
    }

    public AppHost Host { get; }
    public ObservableCollection<PageViewModel> Pages { get; }
    public IReadOnlyList<PageViewModel> PrimaryPages { get; }
    public IReadOnlyList<PageViewModel> UtilityPages { get; }

    [ObservableProperty] private PageViewModel _selected;
    [ObservableProperty] private PageViewModel? _primarySelected;
    [ObservableProperty] private PageViewModel? _utilitySelected;
    [ObservableProperty] private string _drivesStatus = "No drives mounted";
    [ObservableProperty] private string _drivesDetail = "";
    [ObservableProperty] private string _rateStatus = "";
    [ObservableProperty] private string _engineStatus = "";
    [ObservableProperty] private string _gameStatus = "";
    [ObservableProperty] private string _version = "v" + (typeof(App).Assembly.GetName().Version?.ToString(3) ?? "0");

    partial void OnSelectedChanged(PageViewModel value)
    {
        value?.OnShown();
        if (PrimarySelected != value) PrimarySelected = PrimaryPages.Contains(value) ? value : null;
        if (UtilitySelected != value) UtilitySelected = UtilityPages.Contains(value) ? value : null;
    }

    partial void OnPrimarySelectedChanged(PageViewModel? value)
    {
        if (value is not null) Selected = value;
    }

    partial void OnUtilitySelectedChanged(PageViewModel? value)
    {
        if (value is not null) Selected = value;
    }

    public void Navigate(string title)
    {
        var p = Pages.FirstOrDefault(x => x.Title == title);
        if (p != null) Selected = p;
    }

    private void UpdateStatus(EngineMetrics m)
    {
        var sessions = Host.Mounts.Sessions;
        var mounted = sessions.Count(s => s.State == MountState.Mounted);
        DrivesStatus = sessions.Count == 0 ? "No drives mounted"
            : mounted == sessions.Count ? $"{sessions.Count} drive{(sessions.Count == 1 ? "" : "s")} mounted"
            : $"{mounted} of {sessions.Count} drives mounted";
        DrivesDetail = sessions.Count == 0 ? "No virtual drives are mounted."
            : string.Join(Environment.NewLine, sessions.Select(s => $"{s.DriveLetter}: · {s.Definition.DisplayName} · {s.State}"));
        RateStatus = m.DownloadBytesPerSec is { } r && r > 0 ? $"{Bandwidth.BytesPerSecondToMbps(r):0.0} Mbps down" : "";
        EngineStatus = Host.DiskPressure ? "Prefetch paused" : Host.Engine.Scheduler.Active > 0 ? $"Prefetching · {Host.Engine.Queue.Count} queued" : "";
        GameStatus = Host.CurrentLaunch is { State: Games.LaunchState.Running } l ? $"Playing {l.Game.Name}" : "";
    }

    private static void Ui(Action a)
    {
        var d = System.Windows.Application.Current?.Dispatcher;
        if (d is null) return;
        if (d.CheckAccess()) a();
        else d.InvokeAsync(a);
    }
}
