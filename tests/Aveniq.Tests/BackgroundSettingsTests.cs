using System.Text.Json;
using Aveniq.Core;

namespace Aveniq.Tests;

public class BackgroundSettingsTests
{
    [Fact]
    public void Master_switch_overrides_every_individual_preference()
    {
        var settings = new BackgroundSettings
        {
            Enabled = false,
            LaunchAtSignIn = true,
            ShowTrayIcon = true,
            CloseToTray = true,
            Notifications = true
        };

        Assert.Equal(BackgroundEffectiveState.Disabled, BackgroundFeaturePolicy.Resolve(settings));
    }

    [Fact]
    public void Effective_state_requires_master_and_individual_preference()
    {
        var settings = new BackgroundSettings
        {
            Enabled = true,
            LaunchAtSignIn = true,
            ShowTrayIcon = true,
            CloseToTray = true,
            Notifications = true
        };

        Assert.Equal(new BackgroundEffectiveState(true, true, true, true), BackgroundFeaturePolicy.Resolve(settings));

        settings.CloseToTray = false;
        Assert.False(BackgroundFeaturePolicy.Resolve(settings).CloseToTray);

        settings.LaunchAtSignIn = false;
        Assert.False(BackgroundFeaturePolicy.Resolve(settings).AutoStart);

        settings.Notifications = false;
        Assert.False(BackgroundFeaturePolicy.Resolve(settings).Notifications);
    }

    [Fact]
    public void Close_to_tray_requires_the_tray_preference()
    {
        var settings = new BackgroundSettings
        {
            Enabled = true,
            ShowTrayIcon = false,
            CloseToTray = true
        };

        var effective = BackgroundFeaturePolicy.Resolve(settings);
        Assert.False(effective.TrayIcon);
        Assert.False(effective.CloseToTray);
    }

    [Fact]
    public void Master_off_preserves_saved_preferences()
    {
        var settings = new BackgroundSettings
        {
            Enabled = false,
            LaunchAtSignIn = true,
            ShowTrayIcon = true,
            CloseToTray = true,
            Notifications = true
        };

        Assert.False(settings.Enabled);
        Assert.True(settings.LaunchAtSignIn);
        Assert.True(settings.ShowTrayIcon);
        Assert.True(settings.CloseToTray);
        Assert.True(settings.Notifications);
    }

    [Theory]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    public void Window_close_hides_only_when_close_to_tray_and_tray_are_available(
        bool closeToTray, bool trayAvailable, bool expected)
    {
        var effective = new BackgroundEffectiveState(false, trayAvailable, closeToTray, false);
        Assert.Equal(expected, BackgroundLifecyclePolicy.ShouldHideOnClose(effective, trayAvailable));
    }

    [Fact]
    public void Window_close_never_hides_without_a_tray_even_if_misconfigured()
    {
        var effective = new BackgroundEffectiveState(false, true, true, false);
        Assert.False(BackgroundLifecyclePolicy.ShouldHideOnClose(effective, trayAvailable: false));
    }

    [Theory]
    [InlineData(true, true, true, true, true)]
    [InlineData(false, true, true, true, false)]
    [InlineData(true, false, true, true, false)]
    [InlineData(true, true, false, true, false)]
    [InlineData(true, true, true, false, false)]
    public void Hidden_start_requires_sign_in_tray_and_completed_setup(
        bool startedByWindows, bool trayPreference, bool trayAvailable, bool firstRunCompleted, bool expected)
    {
        var effective = new BackgroundEffectiveState(true, trayPreference, true, false);
        Assert.Equal(expected, BackgroundLifecyclePolicy.ShouldStartHidden(
            startedByWindows, effective, trayAvailable, firstRunCompleted));
    }

    [Fact]
    public void Startup_command_quotes_paths_with_spaces()
    {
        var command = BackgroundStartup.BuildCommand(@"C:\Program Files\Aveniq\Aveniq.exe");
        Assert.Equal("\"C:\\Program Files\\Aveniq\\Aveniq.exe\" --background", command);
    }

    [Fact]
    public void Background_startup_argument_is_case_insensitive()
    {
        Assert.True(BackgroundStartup.IsBackgroundLaunch(["--BACKGROUND"]));
        Assert.True(BackgroundStartup.IsBackgroundLaunch(["--background"]));
        Assert.False(BackgroundStartup.IsBackgroundLaunch(["--safe-mode"]));
    }

    [Fact]
    public void Background_settings_round_trip_and_old_files_keep_defaults()
    {
        var dir = Directory.CreateTempSubdirectory("aveniq-background-settings");
        var store = new JsonSettingsStore(Path.Combine(dir.FullName, "settings.json"));
        var settings = new AppSettings();
        settings.Background.Enabled = true;
        settings.Background.LaunchAtSignIn = true;
        settings.Background.CloseToTray = false;

        store.Save(settings);
        var loaded = store.Load();
        Assert.True(loaded.Background.Enabled);
        Assert.True(loaded.Background.LaunchAtSignIn);
        Assert.True(loaded.Background.ShowTrayIcon);
        Assert.False(loaded.Background.CloseToTray);
        Assert.True(loaded.Background.Notifications);

        File.WriteAllText(store.Path, """{"SchemaVersion":1,"RestoreMountsOnStartup":true}""");
        var old = store.Load();
        Assert.NotNull(old.Background);
        Assert.False(old.Background.Enabled);
        Assert.False(old.Background.LaunchAtSignIn);
        Assert.True(old.Background.ShowTrayIcon);
    }

    [Fact]
    public void Null_background_object_does_not_crash_loading()
    {
        var settings = JsonSerializer.Deserialize<AppSettings>("""{"Background":null}""", JsonSettingsStore.Options);
        Assert.NotNull(settings);
        Assert.NotNull(settings.Background);
        Assert.False(settings.Background.Enabled);
    }
}
