using Aveniq.Core;
using Aveniq.Diagnostics;
using Aveniq.Mount;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aveniq.Tests;

public sealed class RcloneConfigServiceTests
{
    [Fact]
    public void SecretStore_loads_a_key_created_by_the_legacy_CloudPlayX_build()
    {
        using var dir = new TempDirectory();
        var path = System.IO.Path.Combine(dir.Path, "legacy.key");
        var legacyEntropy = System.Text.Encoding.UTF8.GetBytes("CloudPlayX.v1.secret");
        var blob = System.Security.Cryptography.ProtectedData.Protect(
            System.Text.Encoding.UTF8.GetBytes("legacy-config-pass"), legacyEntropy,
            System.Security.Cryptography.DataProtectionScope.CurrentUser);
        File.WriteAllBytes(path, blob);

        Assert.Equal("legacy-config-pass", SecretStore.Load(path));
    }

    [Fact]
    public void BaseEnvironment_passes_the_dpapi_key_to_rclone()
    {
        using var dir = new TempDirectory();
        var paths = new AppPaths(dir.Path);
        File.WriteAllText(paths.RcloneConfigFile, "# Encrypted rclone configuration File\n\n");
        SecretStore.Save(paths.ConfigPasswordFile, "test-config-pass");
        var service = new RcloneConfigService(paths, NullLogger<RcloneConfigService>.Instance);

        var env = service.BaseEnvironment();

        Assert.Equal(paths.RcloneConfigFile, env["RCLONE_CONFIG"]);
        Assert.Equal("test-config-pass", env["RCLONE_CONFIG_PASS"]);
    }

    [Fact]
    public void BaseEnvironment_does_not_require_a_key_for_a_plain_config()
    {
        using var dir = new TempDirectory();
        var paths = new AppPaths(dir.Path);
        File.WriteAllText(paths.RcloneConfigFile, "[gdrive]\ntype = drive\n");
        File.WriteAllBytes(paths.ConfigPasswordFile, [1, 2, 3, 4]);
        var service = new RcloneConfigService(paths, NullLogger<RcloneConfigService>.Instance);

        var env = service.BaseEnvironment();

        Assert.Null(env["RCLONE_CONFIG_PASS"]);
    }

    [Fact]
    public void BaseEnvironment_rejects_an_encrypted_config_with_an_unreadable_key()
    {
        using var dir = new TempDirectory();
        var paths = new AppPaths(dir.Path);
        File.WriteAllText(paths.RcloneConfigFile, "# Encrypted rclone configuration File\n\n");
        File.WriteAllBytes(paths.ConfigPasswordFile, [1, 2, 3, 4]);
        var service = new RcloneConfigService(paths, NullLogger<RcloneConfigService>.Instance);

        var ex = Assert.Throws<RcloneConfigKeyException>(() => service.BaseEnvironment());

        Assert.Contains("unreadable", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Windows", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("CRITICAL: Failed to load config file: unable to decrypt configuration and not allowed to ask for password")]
    [InlineData("Failed to load config file: set RCLONE_CONFIG_PASS to your configuration password")]
    public void Decryption_failures_are_classified_as_permanent_config_errors(string stderr)
    {
        Assert.True(RcloneConfigService.IsConfigDecryptionError(stderr));
        Assert.Contains("rclone.conf.key", RcloneConfigService.ExplainError(stderr), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BaseEnvironment_rejects_an_encrypted_config_with_a_missing_key()
    {
        using var dir = new TempDirectory();
        var paths = new AppPaths(dir.Path);
        File.WriteAllText(paths.RcloneConfigFile, "# Encrypted rclone configuration File\n\n");
        var service = new RcloneConfigService(paths, NullLogger<RcloneConfigService>.Instance);

        var ex = Assert.Throws<RcloneConfigKeyException>(() => service.BaseEnvironment());

        Assert.Contains("missing", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(paths.ConfigPasswordFile, ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "aveniq-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            try { Directory.Delete(Path, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }
}
