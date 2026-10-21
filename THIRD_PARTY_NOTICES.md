# Third-party notices

Aveniq bundles or depends on the following components.

## rclone (bundled: `tools/rclone/rclone.exe`, v1.75.2)

<https://rclone.org> — MIT License

```
Copyright (C) 2012 by Nick Craig-Wood http://www.craig-wood.com/nick/

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
```

The binary was downloaded from `https://downloads.rclone.org/v1.75.2/` and its archive was verified against the
official `SHA256SUMS` (included next to the binary).

## TeraBox community rclone fork (optional: `tools/rclone-terabox/rclone.exe`, v1.75.2-extra)

<https://github.com/gulp79/rclone-extra> — MIT License; fork of rclone. This is not an official rclone release and is
not endorsed by TeraBox. The build adds a community TeraBox backend that uses browser session cookies and undocumented
TeraBox web endpoints. It is experimental; see [docs/TERABOX_RESEARCH.md](docs/TERABOX_RESEARCH.md).

The pinned `v1.75.2-extra` Windows x64 release resolves to commit
`56bf13885494819bc801302ddf881279bdac7262`. The release archive SHA-256 is verified against GitHub's release API
and the downloaded bytes by `installer/fetch-rclone-terabox.ps1`. The binary is kept separately from the official
rclone executable. The upstream rclone MIT license text above applies to the fork; source and notices are available
from the linked repository.

## WinFsp (official installer included in `Aveniq-…-setup.exe`, v2.1.25156)

<https://winfsp.dev>, Copyright (C) Bill Zissimopoulos / Navimatics LLC. GPLv3 with a FLOSS exception. Source
code: <https://github.com/winfsp/winfsp>. The setup program carries the unmodified, Authenticode-signed
`winfsp-2.1.25156.msi` from <https://github.com/winfsp/winfsp/releases> and runs it only when WinFsp is missing.
Aveniq does not link WinFsp; rclone uses the WinFsp installation present on the system. WinFsp is a separate
program with its own uninstall entry ("WinFsp 2025").

## NuGet packages

| Package | License |
| --- | --- |
| CommunityToolkit.Mvvm | MIT |
| Microsoft.Data.Sqlite (SQLitePCLRaw, SQLite) | MIT / Public domain |
| Microsoft.Extensions.Logging, DependencyInjection | MIT |
| System.Security.Cryptography.ProtectedData | MIT |
| System.Diagnostics.EventLog | MIT |
| xunit, Microsoft.NET.Test.Sdk, coverlet (tests only) | Apache-2.0 / MIT |

## .NET runtime (bundled in self-contained releases)

MIT License, © .NET Foundation and Contributors.
