using Aveniq.Core;
using Aveniq.Mount;
using Aveniq.Providers;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aveniq.Tests;

public sealed class DropboxConnectorTests
{
    private static readonly RcloneInfo Rclone = new("rclone.exe", "rclone v1.75.2", new Version(1, 75, 2), new HashSet<string>());

    [Theory]
    [InlineData("")]
    [InlineData("has space")]
    [InlineData("dropbox:")]
    public async Task Connect_rejects_invalid_remote_names_before_starting_rclone(string remoteName)
    {
        using var fixture = new ConnectorFixture();

        var error = await Assert.ThrowsAsync<ArgumentException>(() =>
            fixture.Connector.ConnectAsync(Rclone, new DropboxConnector.Request(remoteName, "", ""), null, CancellationToken.None));

        Assert.Contains("Account name", error.Message);
    }

    [Theory]
    [InlineData("app-key", "")]
    [InlineData("", "app-secret")]
    public async Task Connect_rejects_incomplete_custom_app_credentials(string clientId, string clientSecret)
    {
        using var fixture = new ConnectorFixture();

        var error = await Assert.ThrowsAsync<ArgumentException>(() =>
            fixture.Connector.ConnectAsync(Rclone, new DropboxConnector.Request("dropbox", clientId, clientSecret), null, CancellationToken.None));

        Assert.Contains("both the Dropbox app key and app secret", error.Message);
    }

    [Fact]
    public void Dropbox_registry_uses_optional_oauth_credentials_and_official_setup_docs()
    {
        var provider = Assert.Single(ProviderRegistry.All, p => p.Id == "dropbox");

        Assert.Equal(SupportLevel.AvailableUntested, provider.Support);
        Assert.False(provider.RequiresOAuthClient);
        Assert.Equal("https://rclone.org/dropbox/", provider.DocumentationUrl);
        Assert.Contains("built-in Dropbox app", provider.SetupSummary);
    }

    [Fact]
    public void Dropbox_token_errors_explain_how_to_restore_authentication()
    {
        var message = RcloneConfigService.ExplainError("invalid_access_token/.", "dropbox");

        Assert.Contains("Dropbox authentication required", message);
        Assert.Contains("Reconnect the account", message);
    }

    private sealed class ConnectorFixture : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "aveniq-dropbox-test-" + Guid.NewGuid().ToString("N"));

        public ConnectorFixture()
        {
            var paths = new AppPaths(_root);
            var config = new RcloneConfigService(paths, NullLogger<RcloneConfigService>.Instance);
            Connector = new DropboxConnector(config, NullLogger<DropboxConnector>.Instance);
        }

        public DropboxConnector Connector { get; }

        public void Dispose()
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
    }
}
