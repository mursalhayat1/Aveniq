# Background & Startup Implementation Plan

## Goal

Add optional background behavior without changing core mount/rclone lifecycle:
master toggle, per-user auto-start, tray icon, close-to-tray, and sparse notifications.

## Tasks

1. **Core policy and persistence**
   - Add `BackgroundSettings` to `AppSettings` with backward-compatible defaults.
   - Add `BackgroundFeaturePolicy` (master precedence) and `BackgroundLifecyclePolicy` (hide/start-hidden decisions).
   - Add `BackgroundStartup` command construction and startup-argument detection.
   - Add unit tests for the truth table, lifecycle decisions, command quoting, and JSON round trips.

2. **Desktop lifecycle**
   - Add `StartupRegistrationService` using `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`.
   - Add `TrayIconService` using WPF-compatible `System.Windows.Forms.NotifyIcon` and a themed WPF menu.
   - Add `BackgroundCoordinator` to reconcile registration, own the tray icon, and expose effective state/errors.
   - Extend `App` startup for `--background`, hidden startup, tray failure fallback, and activation IPC.
   - Intercept close-to-tray before the existing shutdown prompt; keep explicit Exit on the existing orderly path.

3. **Settings UI**
   - Add a full-width `Background & Startup` card with master and four individual switches.
   - Disable individual controls while master is off, preserve preferences, and show effective status/errors.
   - Apply background changes immediately and also through the existing Save action.

4. **Installer**
   - Delete the per-user `Aveniq` Run value on uninstall without touching user data.

5. **Verification**
   - Build the solution, run unit tests, run the focused integration tests if the environment permits.
   - Manually verify tray restore, close-to-tray, explicit exit, registry enable/disable, hidden startup, and drive preservation.

## Result

Implemented on 2026-10-20.

- `BackgroundSettings` is persisted in the existing settings JSON and old settings files load with safe defaults.
- Effective behavior is resolved in one place: master switch AND preference; close-to-tray also requires the tray preference and a live tray icon.
- Turning the master switch off removes the per-user startup value, disposes the tray icon, and preserves all four saved preferences.
- `--background` launches hidden only when the tray is effective and available and first-run setup is complete. Tray or host initialization failure restores a visible window.
- The tray menu provides Open Aveniq, Settings, and Exit Aveniq. Double-click restores the window; `NotifyIcon` recreates the icon after Explorer restarts.
- A second launch signals the first instance to restore its window instead of starting a second host.
- Notifications are limited to one background balloon per session and all tray/registry failures are surfaced in Settings instead of being claimed as active.
- The Inno Setup uninstaller deletes the `Aveniq` Run value while leaving `%LOCALAPPDATA%\Aveniq` and the cache untouched.

Verification:

- `dotnet build Aveniq.slnx -c Release --nologo -v:minimal`: 0 warnings, 0 errors.
- `dotnet test tests/Aveniq.Tests/Aveniq.Tests.csproj -c Release --nologo -v:minimal`: 93 passed, 0 failed.
- Manual checks completed before desktop control was stopped: card layout/disabled state/status, master on/off, startup registry create/remove/reconcile, hidden `--background` startup with drive reattachment, second-instance restore, close-to-tray and double-click restore, themed tray menu and Settings action, drive preservation.
- Explicit tray **Exit Aveniq** is covered by code review but was not clicked manually after the instruction to stop using the desktop. Its path shares the existing orderly shutdown.

