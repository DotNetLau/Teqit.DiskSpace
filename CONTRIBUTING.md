# Contributing

Thanks for your interest in Teqit.DiskSpace! Issues and pull requests are welcome.

## Ground rules

The project makes three promises. Changes must not break them:

1. **Read-only** - never modify, move or delete user files.
2. **Offline** - no networking, telemetry or update checks. CI fails if networking APIs appear in `src/`.
3. **No data collection** - nothing about the user's files leaves the machine.

## Development

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
dotnet build
dotnet test
dotnet run --project src/Teqit.DiskSpace -- --help
```

The build treats warnings as errors and follows [`.editorconfig`](.editorconfig).

## Pull requests

- Keep them focused: one change per PR.
- Add or update tests for behaviour changes (`tests/Teqit.DiskSpace.Tests`).
- Update the README and [CHANGELOG](CHANGELOG.md) when user-visible behaviour changes.
- Make sure `dotnet test` passes on your machine. CI runs on Windows and Linux.

## Reporting bugs

Please include your OS, the output of `diskspace --version`, the command you ran and what you expected. Redact private paths if you would rather not share them.

By contributing you agree that your contributions are licensed under the [MIT License](LICENSE).
