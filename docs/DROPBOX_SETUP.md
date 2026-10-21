# Connecting Dropbox

Aveniq connects to Dropbox through the official [rclone Dropbox backend](https://rclone.org/dropbox/). Sign-in
happens in your browser; Aveniq never asks for or sees your Dropbox password.

## Connect

1. Open **Cloud Drives → Connect Dropbox**.
2. Keep the suggested account name or change it.
3. Click **Sign in with Dropbox**. Rclone opens Dropbox's authorization page; approve access in your browser.
4. Aveniq verifies the login with a read-only listing of the Dropbox root, encrypts the saved rclone config when
   **Settings → Encrypt the rclone config** is enabled, then mounts the Dropbox root as a drive letter.

You can use **Add virtual drive** later to mount another folder from the same account.

## Optional custom Dropbox app

Rclone's built-in Dropbox app is used by default, which is appropriate for most users. If you prefer your own app,
enable **Use my own Dropbox app key and secret** in the connection window and enter both values. Rclone's Dropbox
documentation describes the app key and OAuth setup: <https://rclone.org/dropbox/>.

The app key and secret are stored only in Aveniq's private rclone config. With config encryption enabled, rclone
encrypts that file and the encryption key is protected with Windows DPAPI for your account. Aveniq does not put
OAuth credentials in process arguments or logs.

## Support status

Dropbox uses rclone's official backend and is available for connection and mounting. However, Aveniq has not yet
verified Dropbox end-to-end with a real account for game streaming. Dropbox content hashes and modification times are
provided by rclone; behavior and compatibility can vary with account type, network conditions, and Dropbox API limits.
