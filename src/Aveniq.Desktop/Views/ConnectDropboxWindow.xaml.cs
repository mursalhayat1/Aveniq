using System.Diagnostics;
using System.Windows;
using Aveniq.App;
using Aveniq.Diagnostics;
using Aveniq.Mount;

namespace Aveniq.Desktop.Views;

public partial class ConnectDropboxWindow : Window
{
    private const string InstructionsUrl = "https://rclone.org/dropbox/";
    private readonly AppHost _host;
    private CancellationTokenSource? _cts;
    private bool _busy;

    public ConnectDropboxWindow(AppHost host)
    {
        _host = host;
        InitializeComponent();
        NameBox.Text = NextName();
    }

    public string RemoteName { get; private set; } = "";

    private string NextName()
    {
        var name = "dropbox";
        for (int i = 2; _host.Remotes.Any(r => r.Name.Equals(name, StringComparison.OrdinalIgnoreCase)); i++)
            name = "dropbox" + i;
        return name;
    }

    private void OpenInstructions(object sender, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo(InstructionsUrl) { UseShellExecute = true });

    private void AdvancedChanged(object sender, RoutedEventArgs e) =>
        AdvancedPanel.Visibility = AdvancedBox.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;

    private async void QuickConnect(object sender, RoutedEventArgs e) =>
        await RunAsync(new DropboxConnector.Request(NameBox.Text.Trim(), "", ""));

    private async void ConnectWithCustomApp(object sender, RoutedEventArgs e) =>
        await RunAsync(new DropboxConnector.Request(NameBox.Text.Trim(), ClientIdBox.Text.Trim(), SecretBox.Password));

    private async Task RunAsync(DropboxConnector.Request request)
    {
        if (_busy) return;
        if (_host.Rclone is null)
        {
            ShowError("rclone was not found; see Diagnostics.");
            return;
        }

        _busy = true;
        _cts = new CancellationTokenSource();
        QuickButton.IsEnabled = ConnectButton.IsEnabled = false;
        Progress.Visibility = Visibility.Visible;
        Status.Foreground = (System.Windows.Media.Brush)FindResource("Accent");
        try
        {
            var progress = new Progress<string>(message => Status.Text = message);
            await _host.DropboxDrive.ConnectAsync(_host.Rclone, request, progress, _cts.Token);
            if (_host.Settings.EncryptRcloneConfig &&
                !await _host.RcloneConfig.EnsureEncryptedAsync(_host.Rclone, _host.AveniqPath, CancellationToken.None))
            {
                try
                {
                    await _host.RcloneConfig.DeleteRemoteAsync(_host.Rclone, request.RemoteName, CancellationToken.None);
                    await _host.RefreshRemotesAsync();
                }
                catch (Exception cleanupError)
                {
                    throw new InvalidOperationException(
                        "Dropbox was connected but its credentials could not be encrypted or removed. Disconnect the account from Cloud Drives.",
                        cleanupError);
                }
                throw new InvalidOperationException("Dropbox was disconnected because its credentials could not be encrypted.");
            }

            RemoteName = request.RemoteName;
            DialogResult = true;
        }
        catch (OperationCanceledException)
        {
            Status.Text = "Cancelled.";
        }
        catch (Exception ex)
        {
            ShowError(Redactor.RedactSecrets(ex.Message, request.ClientId, request.ClientSecret));
        }
        finally
        {
            SecretBox.Clear();
            _busy = false;
            QuickButton.IsEnabled = ConnectButton.IsEnabled = true;
            Progress.Visibility = Visibility.Collapsed;
        }
    }

    private void ShowError(string message)
    {
        Status.Foreground = (System.Windows.Media.Brush)FindResource("Danger");
        Status.Text = message;
    }

    private void Cancel(object sender, RoutedEventArgs e)
    {
        if (_busy && _cts is { IsCancellationRequested: false }) _cts.Cancel();
        else DialogResult = false;
    }
}
