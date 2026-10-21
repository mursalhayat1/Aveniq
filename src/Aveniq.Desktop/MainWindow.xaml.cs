using System.Windows;

namespace Aveniq.Desktop;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        // The default 1380x860 is ideal on a large display, but never open larger
        // than the usable desktop on a smaller laptop.
        var work = SystemParameters.WorkArea;
        Width = Math.Min(Width, Math.Max(MinWidth, work.Width));
        Height = Math.Min(Height, Math.Max(MinHeight, work.Height));
    }
}
