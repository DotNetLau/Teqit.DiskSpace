# Security and privacy

## Privacy guarantees

Teqit.DiskSpace is designed to be trustworthy by construction:

- **Offline.** The application makes no network connections. It has no telemetry, analytics, update checks or crash reporting.
- **Read-only.** It only reads directory listings and file sizes. It never reads file contents and never modifies, moves or deletes anything.
- **Local cache only.** The one file it writes is a JSON snapshot of folder names and sizes (no file names, no contents) in `%LOCALAPPDATA%\Teqit.DiskSpace` (Windows) or `~/.local/share/Teqit.DiskSpace` (Linux). Disable it with `--no-cache`.
- **Only external program:** your own file manager, started only when you choose *Open this folder in file manager*.

## Verify it yourself

The source is small. To check the offline claim:

```bash
grep -rnE "System\.Net|HttpClient|WebClient|TcpClient|UdpClient|Socket" src --include="*.cs" --exclude-dir=obj --exclude-dir=bin
```

This should print nothing. The same check runs on every push and pull request in [CI](.github/workflows/ci.yml), so a change that adds networking cannot be merged unnoticed.

Release binaries are published with SHA-256 checksums (`SHA256SUMS.txt`) on the [Releases page](https://github.com/DotNetLau/Teqit.DiskSpace/releases) and are built from the tagged source by GitHub Actions. If you prefer, build from source instead (see the README).

## Reporting a vulnerability

Please report security issues privately through GitHub's
[private vulnerability reporting](https://github.com/DotNetLau/Teqit.DiskSpace/security/advisories/new)
rather than a public issue. You can expect an initial response within a few days.
