# Releasing

Maintainers' checklist. Versions follow [Semantic Versioning](https://semver.org/).

1. **Pick the version** (`MAJOR.MINOR.PATCH`) and update it in one place: `<Version>` in `src/InternetIPTaskbar/InternetIPTaskbar.csproj`.
2. **Changelog.** In `CHANGELOG.md` move the *Unreleased* entries under a new `## [X.Y.Z] - YYYY-MM-DD` heading and update the compare links at the bottom.
3. **Verify locally.**
   ```powershell
   dotnet build -c Release -p:TreatWarningsAsErrors=true
   dotnet test  -c Release
   .\scripts\fetch-flags.ps1
   .\scripts\build-installer.ps1
   ```
   Install the resulting `artifacts\InternetIPTaskbar_Setup_vX.Y.Z.exe` on a clean machine or VM and check: first run, flag and IP, tray menu, autostart, uninstall.
4. **Commit and tag.**
   ```powershell
   git commit -am "Release X.Y.Z"
   git tag vX.Y.Z
   git push origin main --tags
   ```
5. **GitHub Actions** (`release.yml`) checks that the tag equals the csproj version, runs tests, downloads the flags, builds the installer, writes `SHA256SUMS.txt` and publishes a GitHub release with generated notes. Edit the notes if needed.
6. **After the release.** Start a new empty *Unreleased* section in `CHANGELOG.md`.

## Notes

- Never change `AppId` in `installer/InternetIPTaskbar.iss`: it ties upgrades and uninstall together.
- The installer is not code-signed. If a certificate becomes available, sign `InternetIPTaskbar.exe` and the setup with `signtool` in the release workflow.
- Flag images are fetched at release time and are not stored in git; see `THIRD-PARTY-NOTICES.md`.
