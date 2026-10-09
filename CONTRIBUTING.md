# Contributing to InternetIPTaskbar

Thanks for your interest! Bug reports, ideas, translations and pull requests are all welcome.
By participating you agree to follow the [Code of Conduct](CODE_OF_CONDUCT.md).

## Reporting bugs

Open an issue using the **Bug report** template and include:

- Windows version (e.g. Windows 11 23H2), display scaling, taskbar position;
- InternetIPTaskbar version (shown in the tray menu next to *Exit*);
- what you expected and what happened;
- the relevant part of the log: tray menu -> *Settings -> Verbose log*, reproduce the problem, then *Open log*.
  **The log contains IP addresses - remove anything you do not want to publish.**

Security problems: please do **not** open a public issue, see [SECURITY.md](SECURITY.md).

## Suggesting features

Open an issue using the **Feature request** template. Describe the problem you want to solve before the solution. Keep in mind the project's scope: a small, quiet, privacy-friendly taskbar utility.

## Development setup

You need Windows 10/11, the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) and any editor (Visual Studio 2022, Rider or VS Code).

```powershell
git clone https://github.com/dakeending/InternetIPTaskbar.git
cd InternetIPTaskbar
dotnet build -c Release -p:TreatWarningsAsErrors=true
dotnet test  -c Release
```

Run the app from the build output: `src\InternetIPTaskbar\bin\Release\net8.0-windows\InternetIPTaskbar.exe`.
Only one instance can run at a time (a named mutex). To run a second build next to an installed one while testing, temporarily change the mutex name in `Program.cs` and the data folder in `AppInfo.cs`.

To build the installer you also need [Inno Setup 6](https://jrsoftware.org/isinfo.php): `scripts\build-installer.ps1`.

## Pull requests

1. Fork the repository and create a branch from `main` (`fix/short-name`, `feat/short-name`).
2. Keep the change focused; unrelated cleanups belong in a separate PR.
3. Make sure `dotnet build -c Release -p:TreatWarningsAsErrors=true` and `dotnet test -c Release` pass. CI runs the same.
4. Add or update tests when you change logic that can be tested without a UI.
5. Update `CHANGELOG.md` (the *Unreleased* section) for user-visible changes, and the docs if behaviour changes.
6. Fill in the pull request template.

### Commit messages

Short imperative subject (about 70 characters), a blank line, then an explanation of *why* if it is not obvious.
[Conventional Commits](https://www.conventionalcommits.org/) prefixes (`fix:`, `feat:`, `docs:`) are welcome but not required.

### Code style

- Follow the existing style and `.editorconfig` (4 spaces, UTF-8, file-scoped namespaces, nullable reference types on).
- Comments and identifiers are in English.
- The UI thread must never block: network work happens in `IpMonitor`, results travel as immutable `Snapshot` objects (see [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)).
- Keep dependencies at zero where possible: the app deliberately uses only the .NET base class library.
- Do not add telemetry or network calls to services the user did not expect; any new endpoint must be listed in the README privacy section and in `THIRD-PARTY-NOTICES.md`.

## Translations

All user-visible text is in [`src/InternetIPTaskbar/Strings.cs`](src/InternetIPTaskbar/Strings.cs). Each string is a triple (English, Russian, Traditional Chinese as written in Hong Kong) chosen by `Pick`. The language follows Windows and can be forced in *Settings -> Language*. To add a language, extend `Lang`, `Pick` and the `UiLanguage` enum, supply a translation for every string and add a menu entry; the installer texts are in `installer/InternetIPTaskbar.iss` (`[CustomMessages]`, currently English and Russian). Please keep the English wording simple.

## Releasing (maintainers)

See [docs/RELEASING.md](docs/RELEASING.md).
