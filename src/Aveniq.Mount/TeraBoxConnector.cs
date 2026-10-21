using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Text.Json.Nodes;
using Aveniq.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Aveniq.Mount;

/// <summary>Creates a TeraBox remote in its isolated, encrypted rclone profile.</summary>
public sealed class TeraBoxConnector
{
    private static readonly Regex RemoteNamePattern = new("^[A-Za-z0-9_-]{1,40}$", RegexOptions.CultureInvariant);
    private readonly RcloneConfigService _config;
    private readonly ILogger _log;

    public TeraBoxConnector(RcloneConfigService config, ILogger<TeraBoxConnector> log)
    {
        _config = config;
        _log = log;
    }

    public sealed record Request(string RemoteName, string Cookie);

    public async Task ConnectAsync(RcloneInfo rclone, Request request, string aveniqExePath,
        IProgress<string>? progress, CancellationToken ct)
    {
        if (!rclone.IsSupportedVersion)
            throw new InvalidOperationException($"rclone {rclone.Version} is too old; {RcloneInfo.MinimumVersion} or newer is required.");
        if (!RemoteNamePattern.IsMatch(request.RemoteName))
            throw new ArgumentException("Account name may only contain letters, digits, '-' and '_'.");
        if (string.IsNullOrWhiteSpace(request.Cookie) || request.Cookie.Length > 8192 || request.Cookie.Any(char.IsControl))
            throw new ArgumentException("Enter a valid TeraBox session cookie (maximum 8 KB, with no line breaks).");

        Directory.CreateDirectory(Path.GetDirectoryName(_config.ConfigPath)!);
        if (!File.Exists(_config.ConfigPath))
        {
            using var _ = new FileStream(_config.ConfigPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        }
        if (!await _config.EnsureEncryptedAsync(rclone, aveniqExePath, ct))
            throw new InvalidOperationException("Could not encrypt the TeraBox configuration. No TeraBox credential was saved.");

        var port = RcClient.FindFreeLoopbackPort();
        var user = SecretStore.RandomToken(12);
        var pass = SecretStore.RandomToken(24);
        var env = new Dictionary<string, string?>
        {
            ["RCLONE_RC_USER"] = user,
            ["RCLONE_RC_PASS"] = pass
        };
        var runner = _config.CreateRunner(rclone);
        using var process = new Process
        {
            StartInfo = runner.CreateStartInfo(["rcd", "--rc-addr", $"127.0.0.1:{port}"], env)
        };
        using var rc = new RcClient(port, user, pass, TimeSpan.FromMinutes(2));
        bool creationAttempted = false;
        bool processStarted = false;
        try
        {
            process.OutputDataReceived += (_, _) => { };
            process.ErrorDataReceived += (_, _) => { };
            process.Start();
            processStarted = true;
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            for (int i = 0; i < 50 && !await rc.PingAsync(ct); i++)
            {
                if (process.HasExited) throw new InvalidOperationException("The TeraBox backend process exited during startup.");
                await Task.Delay(100, ct);
            }
            if (!await rc.PingAsync(ct))
                throw new TimeoutException("The local TeraBox configuration service did not start.");

            var existing = await _config.ListRemotesAsync(rclone, ct);
            if (existing.Any(remote => remote.Name.Equals(request.RemoteName, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException($"A TeraBox account named '{request.RemoteName}' already exists.");

            progress?.Report("Saving the encrypted TeraBox connection…");
            creationAttempted = true;
            var result = await rc.CallAsync("config/create", new
            {
                name = request.RemoteName,
                type = "terabox",
                parameters = new Dictionary<string, string>
                {
                    ["cookie"] = request.Cookie.Trim(),
                    ["upload_threads"] = "1",
                    ["debug_level"] = "0"
                },
                opt = new { nonInteractive = true, obscure = false }
            }, ct);
            if (!string.IsNullOrWhiteSpace(result["Error"]?.GetValue<string>()))
                throw new InvalidOperationException("TeraBox configuration was rejected.");

            progress?.Report("Checking TeraBox access with a read-only root listing…");
            _ = await _config.ListDirectoryAsync(rclone, request.RemoteName, "", ct);
            _log.LogInformation("TeraBox remote {Remote} connected and verified with a read-only root listing", request.RemoteName);
        }
        catch (Exception ex)
        {
            if (creationAttempted)
            {
                try
                {
                    var remotes = await _config.ListRemotesAsync(rclone, CancellationToken.None);
                    if (remotes.Any(remote => remote.Name.Equals(request.RemoteName, StringComparison.OrdinalIgnoreCase)))
                        await _config.DeleteRemoteAsync(rclone, request.RemoteName, CancellationToken.None);
                }
                catch (Exception cleanupError)
                {
                    _log.LogError("Could not remove the unverified TeraBox remote {Remote}: {Error}",
                        request.RemoteName, Redactor.RedactSecrets(cleanupError.Message, request.Cookie, request.Cookie.Trim()));
                    throw new InvalidOperationException(
                        "TeraBox verification failed and its saved credentials could not be removed. Disconnect the unverified account from Cloud Drives.");
                }
            }

            if (ex is OperationCanceledException) throw;
            throw new InvalidOperationException(ExplainConnectionError(ex, request.Cookie));
        }
        finally
        {
            if (processStarted)
            {
                try { await rc.CallAsync("core/quit", null, CancellationToken.None, TimeSpan.FromSeconds(3)); }
                catch (Exception ex) when (ex is HttpRequestException or RcException or TaskCanceledException or ObjectDisposedException) { }
                if (!process.HasExited && !process.WaitForExit(3000))
                {
                    try { process.Kill(entireProcessTree: true); }
                    catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
                    {
                        _log.LogWarning("Could not stop the temporary TeraBox configuration process: {Error}",
                            Redactor.Redact(ex.Message));
                    }
                }
            }
        }
    }

    private static string ExplainConnectionError(Exception ex, string cookie)
    {
        var message = Redactor.RedactSecrets(ex.Message, cookie, cookie.Trim());
        if (message.Contains("failed to login", StringComparison.OrdinalIgnoreCase)
            || message.Contains("unauthorized", StringComparison.OrdinalIgnoreCase)
            || message.Contains("invalid cookie", StringComparison.OrdinalIgnoreCase))
            return "TeraBox could not validate this session. It may be expired or revoked; refresh the session in your browser and reconnect.";
        if (message.Contains("no such host", StringComparison.OrdinalIgnoreCase)
            || message.Contains("network", StringComparison.OrdinalIgnoreCase)
            || message.Contains("timeout", StringComparison.OrdinalIgnoreCase))
            return "TeraBox could not be reached. Check the network and try again.";
        return "Could not connect to TeraBox: " + message;
    }
}
