# Teqit.DiskSpace

[![CI](https://github.com/DotNetLau/Teqit.DiskSpace/actions/workflows/ci.yml/badge.svg)](https://github.com/DotNetLau/Teqit.DiskSpace/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![Platforms](https://img.shields.io/badge/platforms-Windows%20%7C%20Linux-lightgrey.svg)](#install)
[![Offline](https://img.shields.io/badge/network-none-brightgreen.svg)](SECURITY.md)

**Find out where your disk space went - in seconds, from the terminal.**

Teqit.DiskSpace (`diskspace`) is a small, cross-platform (Windows and Linux) console tool that scans a drive or folder and shows which folders take up the most space. It is read-only, runs entirely on your machine, and never sends anything anywhere.

```bash
╭─ Teqit DiskSpace ──────────────────────────────────────────────────╮
│ Scan root  C:\Users\you\.nuget                                     │
│ Scanned    31.32 GB in 141,349 files, 60,016 folders (3.3s)        │
│ Drive      774.5 GB used of 931.01 GB, 156.51 GB free (83.2% used) │
╰────────────────────────────────────────────────────────────────────╯
╭──────────┬──────────┬─────────────┬───────────┬────────────────┬─────────╮
│ Folder   │     Size │ % of folder │ % of disk │                │   Files │
├──────────┼──────────┼─────────────┼───────────┼────────────────┼─────────┤
│ packages │ 31.32 GB │        100% │      3.4% │ ██████████████ │ 141,349 │
╰──────────┴──────────┴─────────────┴───────────┴────────────────┴─────────╯
```

*(Real output; the path was shortened. In a terminal the results update live while the scan runs.)*

## Privacy and safety

Please read this first - it is the most important part.

- **No data is extracted.** The tool makes no network connections and has no telemetry, analytics, update checks or crash reporting. Nothing about your files or folders ever leaves your computer.
- **Read-only.** It only reads folder listings and file *sizes*. It never opens, reads, modifies, moves or deletes your files.
- **Folder sizes only.** File contents are never read, and individual file names are not recorded.
- **One local cache file.** After a scan, the folder tree (folder names and sizes) is saved locally so the next start can show results immediately. It is a plain JSON file you can inspect or delete at any time:
  - Windows: `%LOCALAPPDATA%\Teqit.DiskSpace`
  - Linux: `~/.local/share/Teqit.DiskSpace`
  - Disable it entirely with `--no-cache`.
- **The only external program it can start** is your own file manager (`explorer.exe`, `open` or `xdg-open`), and only when you choose *Open this folder in file manager*.
- **Verifiable.** The source is small, and CI fails if networking code ever appears in it. See [SECURITY.md](SECURITY.md) for a one-line check you can run yourself.
- **Open source (MIT).** You are free to use, distribute and adapt it under the [MIT License](#license). The only dependency is [Spectre.Console](https://spectreconsole.net/) for rendering the terminal UI.

## Features

- **Overview** of the scan root (default: the system drive, or `/` on Linux) with size bars and percentages.
- **Top folders, drilled down** - shows `C:\Users\you\AppData\Local\...` rather than just `C:\Users`, so you see the real culprits.
- **Live results** - the scan runs in the background while the screen updates. `⟳` marks folders that are still being scanned. Press `b` or `Enter` to start browsing before the scan finishes.
- **Interactive browser** - drill into any folder, go up, jump back to the scan root, or open the folder in your file manager.
- **Instant start** - the previous scan is shown immediately (marked as out of date) while a fresh scan replaces it.
- **Fast** - folders are scanned in parallel, and small folders are dropped from memory as soon as they are summed.

## Install

### Download a binary

Grab the single-file executable for your platform from the [Releases page](https://github.com/DotNetLau/Teqit.DiskSpace/releases) (`win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`). No .NET installation is needed. Verify the download against `SHA256SUMS.txt` from the same release.

### Build from source

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
git clone https://github.com/DotNetLau/Teqit.DiskSpace.git
cd Teqit.DiskSpace
dotnet run --project src/Teqit.DiskSpace -- [path] [options]
```

To build standalone executables yourself:

```bash
dotnet publish src/Teqit.DiskSpace -c Release -r win-x64   --self-contained -p:PublishSingleFile=true
dotnet publish src/Teqit.DiskSpace -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true
```

macOS is not an official target and is untested, although the code path for opening the file manager (`open`) exists.

## Usage

```bash
diskspace                                  # scan the system drive
diskspace D:\                              # scan another drive
diskspace ~/ --top 50                      # scan your home folder, show 50 folders
diskspace C:\ --exclude C:\Windows --no-interactive
```

When running from source, replace `diskspace` with `dotnet run --project src/Teqit.DiskSpace --`.

| Option | Description |
| --- | --- |
| `[path]`, `-p`, `--path <dir>` | Folder to scan (default: system drive root, or `/` on Linux) |
| `-n`, `--top <count>` | Size of the drilled-down list (default: 30) |
| `--exclude <dir>` | Skip a folder (can be repeated) |
| `--cross-mounts` | Linux: also descend into other mounted filesystems |
| `--no-cache` | Do not read or write the snapshot of the previous scan |
| `--no-interactive` | Print the report and exit (no browser) |
| `--version` | Show the version |
| `-h`, `--help` | Show help |

## How it works

1. The scanner walks the folder tree with a parallel enumeration, summing file sizes per folder. Hidden and system files are included, because that is where disk space often hides.
2. The tree is published while it is being built, so the UI can show provisional numbers immediately.
3. A "drill-down" pass picks the folders that really explain the usage: if one child accounts for almost all of its parent, the child is listed instead of the parent.
4. When the scan finishes, a small snapshot (folder names and sizes) is saved to the local cache so the next start is instant.

## Good to know

- **Run as Administrator / root for complete numbers.** Folders that cannot be accessed are counted and reported rather than hidden.
- Symlinks, junctions and OneDrive cloud placeholders are skipped, so files are not counted twice and cloud-only files are not downloaded.
- On Linux, `/proc`, `/sys`, `/dev`, `/run` and other mounted filesystems are skipped (use `--cross-mounts` to include mounts).
- **Small folders are summarised.** Folders under 1 MB keep their size but not their subfolders, which saves memory on huge drives. They will not appear as drill-down targets.
- Sizes are the sum of logical file sizes. They can differ slightly from what the file manager reports, for example due to disk-cluster rounding, hard links (counted once per link) or compression.

## How does it compare?

Tools such as `du`, `ncdu`, WinDirStat and TreeSize are excellent and well established. Teqit.DiskSpace aims for a different niche:

- one small, self-contained executable with the **same behaviour on Windows and Linux**,
- a **drill-down list** that points at the folder actually responsible (`AppData\Local\...`, not just `Users`),
- **live results** while the scan is still running, and an instant start from the previous scan,
- a strict **read-only, offline** guarantee that CI enforces.

## Contributing

Issues and pull requests are welcome - see [CONTRIBUTING.md](CONTRIBUTING.md) and the [Code of Conduct](CODE_OF_CONDUCT.md). Please keep the project's guarantees intact: read-only, offline, no data collection. Run the tests with:

```bash
dotnet test
```

## License

Teqit.DiskSpace is free, open-source software released under the [MIT License](LICENSE).

You may use it for any purpose, including commercially, and you may copy, modify, merge, publish, distribute, sublicense and sell copies of it. The only requirement is that the copyright notice and licence text are kept in copies or substantial portions of the software. It is provided "as is", without warranty of any kind.
