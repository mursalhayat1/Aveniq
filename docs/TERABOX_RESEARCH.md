# TeraBox — integration research and status

Researched 2026-10-10; integration status reviewed 2026-10-16.

## Current status

Aveniq offers TeraBox as an **optional, experimental, untested** provider through a separate community rclone
build. This does not replace or modify the official `tools/rclone/rclone.exe`; Google Drive and other standard rclone
providers continue to use that binary and the original `%LOCALAPPDATA%\Aveniq\rclone.conf`.

The TeraBox profile is stored separately under `%LOCALAPPDATA%\Aveniq\terabox`. Aveniq encrypts that config
with rclone and protects its random config password with Windows DPAPI, independently of the global config-encryption
setting. A TeraBox browser session cookie is accepted only in the app's local password field; it is not a command-line
argument. Never send the cookie in chat, issue reports, screenshots, or logs. A cookie grants access to the signed-in
account and must be treated like a password.

## Backend and limitations

- The official rclone v1.75.2 build has no TeraBox backend.
- The optional build is `gulp79/rclone-extra`, tag `v1.75.2-extra`, commit
  `56bf13885494819bc801302ddf881279bdac7262`, with the Windows archive SHA-256 pinned in
  `installer/fetch-rclone-terabox.ps1`.
- This is an unsigned community fork, not an official rclone or TeraBox component. Its backend authenticates with
  browser session cookies and uses undocumented TeraBox web endpoints. Those endpoints may change, and use may be
  restricted by TeraBox account policies.
- The connection flow performs only a metadata-only root listing. No real TeraBox account was available during
  implementation; real account verification, nested browsing, upload/download behavior, range requests, mount
  stability, cookie expiry, and game compatibility remain untested.
- Do not rely on it as the only copy of important data or for a production game library. Disconnect and reconnect
  locally if the session expires. Do not disable config encryption to troubleshoot it.

The backend's documentation advertises range reads and write operations, but those claims have not been independently
validated. A successful root listing does not establish compatibility with Aveniq's virtual-drive or game
streaming requirements.

## Build provenance

`installer/fetch-rclone-terabox.ps1` checks the release tag, resolved commit, and GitHub-published plus downloaded
archive SHA-256 before extracting the binary to `tools/rclone-terabox`. It never writes over the official rclone
binary and refuses to replace an existing TeraBox executable with different contents. Release builds verify its
provenance metadata and executable hash before packaging it beside the official binary.

See [TERABOX_SETUP.md](TERABOX_SETUP.md) for the local UI flow and [THIRD_PARTY_NOTICES.md](../THIRD_PARTY_NOTICES.md)
for licensing and provenance.
