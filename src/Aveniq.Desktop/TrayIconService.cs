using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using DrawingIcon = System.Drawing.Icon;
using Forms = System.Windows.Forms;
using Microsoft.Extensions.Logging;
using WpfApplication = System.Windows.Application;

namespace Aveniq.Desktop;

/// <summary>
/// Owns the notification-area icon and its WPF context menu. Events are raised on the WPF UI thread
/// because the NotifyIcon is created there. The icon is disposed when background features are paused.
/// </summary>
public sealed class TrayIconService : IDisposable
{
    private readonly Action _open;
    private readonly Action _settings;
    private readonly Action _exit;
    private readonly ILogger _log;
    private Forms.NotifyIcon? _notifyIcon;
    private ContextMenu? _menu;
    private DrawingIcon? _icon;
    private bool _available;

    public TrayIconService(ILogger log, Action open, Action settings, Action exit)
    {
        _log = log;
        _open = open;
        _settings = settings;
        _exit = exit;
    }

    public bool Available => _available;
    public string? Error { get; private set; }

    public bool Initialize()
    {
        if (_available) return true;
        try
        {
            Error = null;
            _icon ??= LoadAppIcon();
            _menu = BuildMenu();
            _notifyIcon = new Forms.NotifyIcon
            {
                Icon = _icon,
                Text = "Aveniq — cloud drives",
                Visible = false
            };
            _notifyIcon.MouseDoubleClick += OnDoubleClick;
            _notifyIcon.MouseUp += OnMouseUp;
            _notifyIcon.BalloonTipClicked += OnBalloonTipClicked;
            _notifyIcon.Visible = true;
            _available = true;
            return true;
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            DisposeCore();
            return false;
        }
    }

    public bool ShowNotification(string title, string message)
    {
        if (!_available || _notifyIcon is null) return false;
        try
        {
            _notifyIcon.ShowBalloonTip(5000, title, message, Forms.ToolTipIcon.Info);
            return true;
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            return false;
        }
    }

    public void Dispose()
    {
        _available = false;
        DisposeCore();
    }

    private ContextMenu BuildMenu()
    {
        var menu = new ContextMenu { StaysOpen = false };
        var menuStyle = WpfApplication.Current?.TryFindResource("TrayContextMenu") as Style;
        if (menuStyle is null) _log.LogWarning("Tray context menu style 'TrayContextMenu' was not found");
        else menu.Style = menuStyle;

        menu.Items.Add(CreateItem("Open Aveniq", _open));
        menu.Items.Add(CreateItem("Settings", _settings));
        menu.Items.Add(new Separator
        {
            Style = WpfApplication.Current?.TryFindResource("TraySeparator") as Style
        });
        menu.Items.Add(CreateItem("Exit Aveniq", _exit));
        return menu;
    }

    private MenuItem CreateItem(string header, Action action)
    {
        var item = new MenuItem { Header = header };
        var itemStyle = WpfApplication.Current?.TryFindResource("TrayMenuItem") as Style;
        if (itemStyle is null) _log.LogWarning("Tray menu item style 'TrayMenuItem' was not found");
        else item.Style = itemStyle;
        item.Click += (_, _) => InvokeSafely(action, header);
        return item;
    }

    private void OnDoubleClick(object? sender, Forms.MouseEventArgs e)
    {
        if (e.Button != Forms.MouseButtons.Left) return;
        CloseMenu();
        InvokeSafely(_open, "tray open");
    }

    private void OnMouseUp(object? sender, Forms.MouseEventArgs e)
    {
        if (e.Button != Forms.MouseButtons.Right || _menu is null) return;
        _menu.Placement = PlacementMode.MousePoint;
        // The notification area sits at the bottom/right of the screen. Offset the menu up and to the
        // left of the cursor so the taskbar overflow flyout cannot cover the first item.
        _menu.HorizontalOffset = -140;
        _menu.VerticalOffset = -80;
        try
        {
            _menu.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var width = _menu.DesiredSize.Width;
            if (width > 0) _menu.HorizontalOffset = -width - 10;
            _menu.IsOpen = true;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Tray context menu could not be opened");
        }
    }

    private void OnBalloonTipClicked(object? sender, EventArgs e) => InvokeSafely(_open, "notification click");

    private void InvokeSafely(Action action, string actionName)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Tray action '{Action}' failed", actionName);
        }
    }

    private void CloseMenu()
    {
        var menu = _menu;
        if (menu is null) return;
        try
        {
            menu.IsOpen = false;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Tray context menu could not be closed");
        }
    }

    private void DisposeCore()
    {
        try
        {
            CloseMenu();
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Tray context menu could not be closed during disposal");
        }

        _menu = null;

        var notifyIcon = _notifyIcon;
        _notifyIcon = null;
        if (notifyIcon is not null)
        {
            try
            {
                notifyIcon.Visible = false;
                notifyIcon.MouseDoubleClick -= OnDoubleClick;
                notifyIcon.MouseUp -= OnMouseUp;
                notifyIcon.BalloonTipClicked -= OnBalloonTipClicked;
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Tray icon could not be hidden cleanly");
            }

            try
            {
                notifyIcon.Dispose();
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Tray icon could not be disposed cleanly");
            }
        }

        var icon = _icon;
        _icon = null;
        if (icon is not null)
        {
            try
            {
                icon.Dispose();
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Tray icon bitmap could not be disposed cleanly");
            }
        }
    }

    /// <summary>Use the embedded application icon, with the process icon and a generic icon as fallbacks.</summary>
    public static DrawingIcon LoadAppIcon()
    {
        try
        {
            var resource = WpfApplication.GetResourceStream(new Uri("pack://application:,,,/Assets/aveniq.ico"));
            if (resource?.Stream is { } stream)
            {
                using (stream) return new DrawingIcon(stream);
            }
        }
        catch (Exception)
        {
            // Fall through to the executable icon; the tray must not crash because a resource is missing.
        }

        try
        {
            var path = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path) &&
                System.Drawing.Icon.ExtractAssociatedIcon(path) is { } extracted)
            {
                return extracted;
            }
        }
        catch (Exception)
        {
            // Fall through to the generic application icon.
        }

        return (DrawingIcon)System.Drawing.SystemIcons.Application.Clone();
    }
}
