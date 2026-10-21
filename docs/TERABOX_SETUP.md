# Experimental TeraBox setup

TeraBox support uses a pinned, unsigned community rclone fork rather than an official TeraBox API. It is experimental
and has not been validated with a real account or game. Read [the research and limitations](TERABOX_RESEARCH.md)
before using it.

## Connect locally

1. Install the release package that includes `tools\rclone-terabox\rclone.exe`. A source build must first run
   `installer\fetch-rclone-terabox.ps1`.
2. Open **Cloud Drives → Connect TeraBox**.
3. Enter a name and the browser session cookie for your own signed-in TeraBox account in the password field. Follow
   the backend's [TeraBox setup instructions](https://github.com/gulp79/rclone-extra/blob/v1.75.2-extra/docs/content/terabox.md)
   to obtain the value. The cookie is a credential: do not paste it into chat, email, screenshots, support requests,
   or diagnostic reports.
4. Select **Connect**. Aveniq encrypts the separate TeraBox profile before saving the credential, then attempts a
   metadata-only root listing. If the session is expired or the endpoint is unavailable, refresh your browser session
   and reconnect in the app.
5. After a successful check, add a virtual drive and select the TeraBox folder you need.

The TeraBox config is stored in `%LOCALAPPDATA%\Aveniq\terabox\rclone.conf`; its encryption key is protected with
Windows DPAPI for the current Windows account. It is isolated from Google Drive's config and is not affected by the
global rclone-config encryption setting. Keep your Windows account secured.

## Limitations and safe use

- A root listing is the only TeraBox operation exercised by the connection check. File transfers, writes, range reads,
  nested folder browsing, mount recovery, and game streaming are not yet verified against a real account.
- The backend relies on undocumented web endpoints and browser cookies. TeraBox may change or revoke them.
- Use an account and data you can afford to make temporarily unavailable. Keep independent backups of important files.
- Disconnecting removes the saved local remote; it does not delete cloud files. Removing a virtual drive does not delete
  cloud files either.
- If access fails, reconnect with a refreshed session locally. Do not send the cookie to anyone or turn on verbose
  logging to troubleshoot it.
