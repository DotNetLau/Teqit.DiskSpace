# Changelog

All notable changes to this project are documented here. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project follows
[Semantic Versioning](https://semver.org/).

## [Unreleased]

### Added
- `--version` option.
- Continuous integration (Windows and Linux), tag-driven releases with checksums, and a CI check that the source stays free of networking APIs.
- Project documentation: `SECURITY.md`, `CONTRIBUTING.md`, `CODE_OF_CONDUCT.md`, issue and pull request templates.
- Tests for command-line parsing, symlink skipping and hidden files.

### Changed
- The executable is now named `diskspace` (also installable as a .NET tool).
- The build treats warnings as errors; test dependencies updated.

### Fixed
- Redirected output (`diskspace ... > file`, pipes) no longer squeezes the "top folders" table into unreadable cells.

## [1.0.0]

### Added
- Parallel disk scan with live, provisional results while scanning.
- Overview and "top folders, drilled down" report.
- Interactive folder browser with "open in file manager".
- Snapshot cache of the previous scan for an instant start (`--no-cache` to disable).
