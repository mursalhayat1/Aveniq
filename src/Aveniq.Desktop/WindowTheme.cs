using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace Aveniq.Desktop;

/// <summary>
/// Applies the Aveniq graphite palette to the native window frame. This keeps the
/// standard Windows caption buttons and resize behaviour instead of replacing them.
/// </summary>
public static class WindowTheme
{
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaUseImmersiveDarkModeLegacy = 19;
    private const int DwmwaBorderColor = 34;
    private const int DwmwaCaptionColor = 35;
    private const int DwmwaTextColor = 36;
    private static bool _registered;

    public static void EnableForAllWindows()
    {
        if (_registered) return;
        _registered = true;
        EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent,
            new RoutedEventHandler((sender, _) => Apply(sender as Window)));
    }

    public static void Apply(Window? window)
    {
        if (window is null) return;
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;

        var dark = 1;
        _ = DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref dark, sizeof(int));
        _ = DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkModeLegacy, ref dark, sizeof(int));

        var caption = window.Background is SolidColorBrush bg ? Bgr(bg.Color) : 0x000C0806;
        var text = window.TryFindResource("TextColor") is Color tc ? Bgr(tc) : 0x00F8F1EC;
        var border = window.TryFindResource("BorderColor") is Color bc ? Bgr(bc) : 0x0030241C;
        _ = DwmSetWindowAttribute(hwnd, DwmwaCaptionColor, ref caption, sizeof(int));
        _ = DwmSetWindowAttribute(hwnd, DwmwaTextColor, ref text, sizeof(int));
        _ = DwmSetWindowAttribute(hwnd, DwmwaBorderColor, ref border, sizeof(int));
    }

    private static int Bgr(Color c) => c.B | c.G << 8 | c.R << 16;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
