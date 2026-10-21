using System.Diagnostics;
using System.Text.RegularExpressions;
using Aveniq.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Aveniq.Mount;

/// <summary>Creates and verifies a Dropbox remote through rclone's OAuth configuration flow.</summary>
public sealed class DropboxConnector
{
    private static readonly Regex RemoteNamePattern = new("^[A-Za-z0-9_-]{1,40}$", RegexOptions.CultureInvariant);
    private readonly RcloneConfigService _config;
    private readonly ILogger _log;

    public DropboxConnector(RcloneConfigService config, ILogger<DropboxConnector> log)
    {
        _config = config;
        _log = log;
    }

    public sealed record Request(string RemoteName, string ClientId, string ClientSecret)
    {
        public bool UsesDefaultClient => string.IsNullOrWhiteSpace(ClientId) && string.IsNullOrWhiteSpace(ClientSecret);
    }

    public async Task ConnectAsync(RcloneInfo rclone, Request request, IProgress<string>? progress, CancellationToken ct)
    {
        if (!rclone.IsSupportedVersion)
            throw new InvalidOperationException($"rclone {rclone.Version} is too old; {RcloneInfo.MinimumVersion} or newer is required.");
        if (string.IsNullOrWhiteSpace(request.RemoteName) || !RemoteNamePattern.IsMatch(request.RemoteName))
            throw new ArgumentException("Account name may only contain letters, digits, '-' and '_'.");

        var clientId = request.ClientId?.Trim() ?? "";
        var clientSecret = request.ClientSecret?.Trim() ?? "";
        if (string.IsNullOrEmpty(clientId) != string.IsNullOrEmpty(clientSecret))
            throw new ArgumentException("Enter both the Dropbox app key and app secret, or leave both blank to use rclone's built-in app.");

        var port = RcClient.FindFreeLoopbackPort();
        var user = SecretStore.RandomToken(12);
        var pass = SecretStore.RandomToken(24);
        var env = new Dictionary<string, string?>(_config.BaseEnvironment())
        {
            ["RCLONE_RC_USER"] = user,
            ["RCLONE_RC_PASS"] = pass
        };
        var runner = new RcloneRunner(rclone.Path, env);
        using var process = new Process
        {
            StartInfo = runner.CreateStartInfo(["rcd", "--rc-addr", $"127.0.0.1:{port}"])
        };
        using var rc = new RcClient(port, user, pass, TimeSpan.FromMinutes(10));
        bool processStarted = false;
        bool creationAttempted = false;

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
                if (process.HasExited) throw new InvalidOperationException("The Dropbox configuration process exited during startup.");
                await Task.Delay(100, ct);
            }
            if (!await rc.PingAsync(ct))
                throw new TimeoutException("The local Dropbox configuration service did not start.");

            var existing = await _config.ListRemotesAsync(rclone, ct);
            if (existing.Any(remote => remote.Name.Equals(request.RemoteName, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException($"A Dropbox account named '{request.RemoteName}' already exists.");

            var parameters = new Dictionary<string, string>();
            if (!request.UsesDefaultClient)
            {
                parameters["client_id"] = clientId;
                parameters["client_secret"] = clientSecret;
            }

            progress?.Report("Opening Dropbox sign-in in your browser…");
            creationAttempted = true;
            var node = await rc.CallAsync("config/create", new
            {
                name = request.RemoteName,
                type = "dropbox",
                parameters,
                opt = new { nonInteractive = true, obscure = false }
            }, ct);

            bool completed = false;
            for (int step = 0; step < 12; step++)
            {
                var state = node["State"]?.GetValue<string>() ?? "";
                var error = node["Error"]?.GetValue<string>();
                if (!string.IsNullOrWhiteSpace(error))
                    throw new InvalidOperationException("Dropbox setup failed: " + Redactor.RedactSecrets(error, clientId, clientSecret));
                if (string.IsNullOrEmpty(state))
                {
                    completed = true;
                    break;
                }

                var option = node["Option"]?["Name"]?.GetValue<string>() ?? "";
                var answer = option == "config_is_local"
                    ? "true"
                    : node["Option"]?["DefaultStr"]?.GetValue<string>() ?? "";
                if (option != "config_is_local")
                    progress?.Report($"Finishing Dropbox configuration ({option})…");
                _log.LogInformation("Dropbox config step {Option}", option);
                node = await rc.CallAsync("config/update", new
                {
                    name = request.RemoteName,
                    parameters = new Dictionary<string, string>(),
                    opt = new { nonInteractive = true, obscure = false, @continue = true, state, result = answer }
                }, ct, TimeSpan.FromMinutes(10));
            }
            if (!completed)
                throw new InvalidOperationException("Dropbox configuration did not complete.");

            progress?.Report("Verifying Dropbox access with a read-only root listing…");
            _ = await _config.ListDirectoryAsync(rclone, request.RemoteName, "", ct);
            _log.LogInformation("Dropbox remote {Remote} connected and verified with a read-only root listing", request.RemoteName);
        }
        catch (Exception ex)
        {
            if (creationAttempted)
                await RemoveUnverifiedRemoteAsync(rclone, request.RemoteName, clientId, clientSecret);

            if (ex is OperationCanceledException) throw;
            throw new InvalidOperationException(ExplainConnectionError(ex, clientId, clientSecret), ex);
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
                        _log.LogWarning("Could not stop the temporary Dropbox configuration process: {Error}", Redactor.Redact(ex.Message));
                    }
                }
            }
        }
    }

    private async Task RemoveUnverifiedRemoteAsync(RcloneInfo rclone, string remoteName, string clientId, string clientSecret)
    {
        try
        {
            var remotes = await _config.ListRemotesAsync(rclone, CancellationToken.None);
            if (remotes.Any(remote => remote.Name.Equals(remoteName, StringComparison.OrdinalIgnoreCase)))
                await _config.DeleteRemoteAsync(rclone, remoteName, CancellationToken.None);
        }
        catch (Exception cleanupError)
        {
            _log.LogError("Could not remove the unverified Dropbox remote {Remote}: {Error}",
                remoteName, Redactor.RedactSecrets(cleanupError.Message, clientId, clientSecret));
            throw new InvalidOperationException(
                "Dropbox verification failed and its saved credentials could not be removed. Disconnect the unverified account from Cloud Drives.",
                cleanupError);
        }
    }

    private static string ExplainConnectionError(Exception ex, string clientId, string clientSecret)
    {
        var message = Redactor.RedactSecrets(ex.Message, clientId, clientSecret);
        if (message.Contains("access_denied", StringComparison.OrdinalIgnoreCase)
            || message.Contains("invalid_grant", StringComparison.OrdinalIgnoreCase)
            || message.Contains("unauthorized", StringComparison.OrdinalIgnoreCase))
            return "Dropbox did not authorize this sign-in. Check the account and app permissions, then try again.";
        if (message.Contains("no such host", StringComparison.OrdinalIgnoreCase)
            || message.Contains("network", StringComparison.OrdinalIgnoreCase)
            || message.Contains("timeout", StringComparison.OrdinalIgnoreCase))
            return "Dropbox could not be reached. Check the network and try again.";
        return "Could not connect to Dropbox: " + message;
    }
}
