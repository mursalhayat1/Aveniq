namespace Aveniq.Providers;

public enum SupportLevel
{
    /// <summary>Implemented and exercised by Aveniq's own workflow (connect, mount, stream).</summary>
    Supported,
    /// <summary>Works through the corresponding rclone backend; not tested end-to-end by Aveniq.</summary>
    AvailableUntested,
    /// <summary>No authorized integration exists; the adapter is a placeholder that explains why.</summary>
    Unsupported
}

public sealed record ProviderCapabilities(bool RangeReads, bool Checksums, bool Writes, bool ChangeNotifications, string? Notes = null);

/// <summary>Describes one cloud backend and how Aveniq connects to it.</summary>
public interface ICloudProvider
{
    string Id { get; }
    string DisplayName { get; }
    string? RcloneBackend { get; }
    SupportLevel Support { get; }
    ProviderCapabilities Capabilities { get; }
    bool RequiresOAuthClient { get; }
    string SetupSummary { get; }
    string? DocumentationUrl { get; }
}

public sealed record CloudProvider(
    string Id, string DisplayName, string? RcloneBackend, SupportLevel Support, ProviderCapabilities Capabilities,
    bool RequiresOAuthClient, string SetupSummary, string? DocumentationUrl) : ICloudProvider;

public static class ProviderRegistry
{
    public static readonly CloudProvider GoogleDrive = new(
        "gdrive", "Google Drive", "drive", SupportLevel.Supported,
        new ProviderCapabilities(RangeReads: true, Checksums: true, Writes: true, ChangeNotifications: true,
            "MD5 checksums and modification times are used for cache validation; Drive API quotas apply (rate limits are retried with backoff by rclone)."),
        RequiresOAuthClient: true,
        "Create your own OAuth client in Google Cloud Console (Desktop app type), enable the Google Drive API, then paste the client ID and secret into Aveniq. " +
        "rclone's shared client ID is being retired during 2026, so your own client is required.",
        "https://rclone.org/drive/#making-your-own-client-id");

    public static readonly CloudProvider TeraBox = new(
        "terabox", "TeraBox", "terabox", SupportLevel.AvailableUntested,
        new ProviderCapabilities(true, true, true, false,
            "Uses an unofficial community rclone backend and browser session cookie. API stability and account behavior are not verified by Aveniq; zero-byte uploads are unsupported."),
        RequiresOAuthClient: false,
        "Connect with a TeraBox browser session cookie. Treat it like a password; it is stored only in the isolated encrypted TeraBox profile.",
        "https://github.com/gulp79/rclone-extra/blob/v1.75.2-extra/docs/content/terabox.md");

    public static IReadOnlyList<CloudProvider> All { get; } =
    [
        GoogleDrive,
        new("onedrive", "Microsoft OneDrive", "onedrive", SupportLevel.AvailableUntested,
            new ProviderCapabilities(true, true, true, true), true,
            "Configure with 'rclone config' (backend 'onedrive') using Aveniq's config file, then add it in Cloud Drives.", "https://rclone.org/onedrive/"),
        new("dropbox", "Dropbox", "dropbox", SupportLevel.AvailableUntested,
            new ProviderCapabilities(true, true, true, true,
                "The official rclone backend supports Dropbox content hashes and modification times; end-to-end game streaming has not yet been verified."),
            false,
            "Connect in Cloud Drives with browser OAuth. Rclone's built-in Dropbox app works by default; an optional custom app key and secret can be supplied.",
            "https://rclone.org/dropbox/"),
        new("s3", "S3-compatible storage", "s3", SupportLevel.AvailableUntested,
            new ProviderCapabilities(true, true, true, false), false,
            "Configure with 'rclone config' (backend 's3'); access keys stay in the encrypted rclone config.", "https://rclone.org/s3/"),
        new("nas", "Local NAS (SMB/SFTP)", "smb", SupportLevel.AvailableUntested,
            new ProviderCapabilities(true, false, true, false), false,
            "Configure with 'rclone config' (backend 'smb' or 'sftp').", "https://rclone.org/smb/"),
        TeraBox,
    ];

    public static CloudProvider? ForBackend(string rcloneType) =>
        All.FirstOrDefault(p => string.Equals(p.RcloneBackend, rcloneType, StringComparison.OrdinalIgnoreCase));
}
