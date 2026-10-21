namespace Aveniq.Core;

/// <summary>
/// File-system locations used by Aveniq. The data root defaults to %LOCALAPPDATA%\Aveniq and can be
/// redirected with the AVENIQ_HOME environment variable (portable mode, tests).
/// </summary>
public sealed class AppPaths
{
    public AppPaths(string? dataRoot = null)
    {
        DataRoot = dataRoot ?? ResolveDefaultDataRoot();
        RenameLegacyDatabaseFiles(DataRoot);
        Directory.CreateDirectory(DataRoot);
        Directory.CreateDirectory(LogDirectory);
    }

    public string DataRoot { get; }
    public string TeraBoxDataRoot => Path.Combine(DataRoot, "terabox");
    public string SettingsFile => Path.Combine(DataRoot, "settings.json");
    public string DatabaseFile => Path.Combine(DataRoot, "aveniq.db");
    public string LogDirectory => Path.Combine(DataRoot, "logs");
    public string RcloneConfigFile => Path.Combine(DataRoot, "rclone.conf");
    public string ConfigPasswordFile => Path.Combine(DataRoot, "rclone.conf.key");
    public string MountStateFile => Path.Combine(DataRoot, "mounts.state.json");
    public string DefaultCacheRoot => Path.Combine(DataRoot, "cache");
    public string DefaultLocalGamesRoot => Path.Combine(DataRoot, "local-games");
    public string DiagnosticsDirectory => Path.Combine(DataRoot, "diagnostics");

    public string CacheRootFor(AppSettings s) => string.IsNullOrWhiteSpace(s.Cache.CacheRoot) ? DefaultCacheRoot : s.Cache.CacheRoot!;

    public string MountCacheDir(AppSettings s, string mountId) => Path.Combine(CacheRootFor(s), "m-" + mountId);

    public string LocalGamesRootFor(AppSettings s) => string.IsNullOrWhiteSpace(s.LocalGamesRoot) ? DefaultLocalGamesRoot : s.LocalGamesRoot!;

    /// <summary>Directory containing the running executables (bundled rclone lives under tools\rclone).</summary>
    public static string AppDirectory => AppContext.BaseDirectory;

    private static string ResolveDefaultDataRoot()
    {
        var configuredRoot = Environment.GetEnvironmentVariable("AVENIQ_HOME");
        if (!string.IsNullOrWhiteSpace(configuredRoot))
            return configuredRoot;

        var legacyConfiguredRoot = Environment.GetEnvironmentVariable("CLOUDPLAYX_HOME");
        if (!string.IsNullOrWhiteSpace(legacyConfiguredRoot))
            return legacyConfiguredRoot;

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dataRoot = Path.Combine(localAppData, "Aveniq");
        var legacyDataRoot = Path.Combine(localAppData, "CloudPlayX");
        if (Directory.Exists(legacyDataRoot))
        {
            if (Directory.Exists(dataRoot))
                throw new InvalidOperationException(
                    $"Both Aveniq and legacy application data folders exist. Back up and merge '{legacyDataRoot}' into '{dataRoot}', then restart Aveniq.");

            try
            {
                Directory.Move(legacyDataRoot, dataRoot);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new InvalidOperationException(
                    $"Could not migrate existing application data from '{legacyDataRoot}' to '{dataRoot}'. Close other Aveniq instances and try again.",
                    ex);
            }
        }

        return dataRoot;
    }

    private static void RenameLegacyDatabaseFiles(string dataRoot)
    {
        foreach (var suffix in new[] { "", "-wal", "-shm" })
        {
            var oldPath = Path.Combine(dataRoot, "cloudplayx.db" + suffix);
            if (!File.Exists(oldPath))
                continue;

            var newPath = Path.Combine(dataRoot, "aveniq.db" + suffix);
            if (File.Exists(newPath))
                throw new IOException($"Both legacy and Aveniq database files exist. Back up and resolve '{oldPath}' and '{newPath}' before starting Aveniq.");
            File.Move(oldPath, newPath);
        }
    }
}
