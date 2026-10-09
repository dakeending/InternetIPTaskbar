## What and why

<!-- What does this change do, and what problem does it solve? Link the issue: "Fixes #123". -->

## How was it tested

<!-- Unit tests, manual steps, Windows version / DPI / taskbar position if relevant. -->

## Checklist

- [ ] `dotnet build -c Release -p:TreatWarningsAsErrors=true` and `dotnet test -c Release` pass
- [ ] Tests added or updated (where the logic can be tested without UI)
- [ ] `CHANGELOG.md` updated (*Unreleased*) for user-visible changes
- [ ] Docs updated if behaviour changed
- [ ] No new network endpoints, or they are listed in the README privacy section and `THIRD-PARTY-NOTICES.md`
- [ ] New UI text is added to `Strings.cs` in English, Russian and Traditional Chinese
