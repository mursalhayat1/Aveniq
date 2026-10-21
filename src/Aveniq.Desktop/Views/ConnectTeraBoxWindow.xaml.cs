using System.Diagnostics;
using System.Windows;
using Aveniq.App;
using Aveniq.Diagnostics;
using Aveniq.Mount;

namespace Aveniq.Desktop.Views;

public partial class ConnectTeraBoxWindow : Window
{
    private const string InstructionsUrl = "https://github.com/gulp79/rclone-extra/blob/v1.75.2-extra/docs/content/terabox.md";
    private readonly AppHost _host;
    private CancellationTokenSource? _cts;
    private bool _busy;

    public ConnectTeraBoxWindow(AppHost host)
    {
        _host = host;
        InitializeComponent();
        NameBox.Text = NextName();
        ConnectButton.IsEnabled = host.TeraBoxRclone is not null;
        if (host.TeraBoxRclone is null)
            Status.Text = "The verified TeraBox community backend is not installed. Install the Aveniq package that includes it before connecting.";
    }

    public string RemoteName { get; private set; } = "";

    private string NextName()
    {
        var name = "terabox";
        for (int i = 2; _host.Remotes.Any(r => r.Name.Equals(name, StringComparison.OrdinalIgnoreCase)); i++)
            name = "terabox" + i;
        return name;
    }

    private void OpenInstructions(object sender, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo(InstructionsUrl) { UseShellExecute = true });

    private async void Connect(object sender, RoutedEventArgs e)
    {
        if (_busy || _host.TeraBoxRclone is null) return;
        _busy = true;
        _cts = new CancellationTokenSource();
        ConnectButton.IsEnabled = false;
        Progress.Visibility = Visibility.Visible;
        Status.Text = "Preparing the encrypted TeraBox profile…";
        try
        {
            var progress = new Progress<string>(message => Status.Text = message);
            await _host.TeraBoxDrive.ConnectAsync(
                _host.TeraBoxRclone,
                new TeraBoxConnector.Request(NameBox.Text.Trim(), CookieBox.Password),
                _host.AveniqPath,
                progress,
                _cts.Token);
            RemoteName = NameBox.Text.Trim();
            CookieBox.Clear();
            DialogResult = true;
        }
        catch (OperationCanceledException)
        {
            Status.Text = "Cancelled.";
        }
        catch (Exception ex)
        {
            Status.Foreground = (System.Windows.Media.Brush)FindResource("Danger");
            Status.Text = Redactor.Redact(ex.Message);
        }
        finally
        {
            CookieBox.Clear();
            _busy = false;
            ConnectButton.IsEnabled = _host.TeraBoxRclone is not null;
            Progress.Visibility = Visibility.Collapsed;
        }
    }

    private void Cancel(object sender, RoutedEventArgs e)
    {
        if (_busy && _cts is { IsCancellationRequested: false }) _cts.Cancel();
        else DialogResult = false;
    }
}
