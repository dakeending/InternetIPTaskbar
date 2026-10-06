# Security Policy

## Supported versions

Only the latest released version receives security fixes.

| Version | Supported |
|---|---|
| latest 2.x | yes |
| older | no |

## Reporting a vulnerability

Please **do not** report security problems in public issues.

Use GitHub's private reporting: open the repository's **Security** tab and choose **Report a vulnerability**
(<https://github.com/dakeending/InternetIPTaskbar/security/advisories/new>).
If that is unavailable, contact the maintainer through the options on their GitHub profile and ask for a private channel before sending details.

Please include the affected version, steps to reproduce and the impact you expect.
You can expect an acknowledgement within a few days. Fixes are released as a new version and credited in the changelog unless you prefer to stay anonymous.

## Scope and threat model

InternetIPTaskbar is a local user-mode utility. Things we consider in scope:

- code execution or privilege escalation through the installer, uninstaller or the application;
- leaking the user's data (the remembered "real" IP, logs) to anyone other than the services listed in the README;
- unsafe handling of data returned by the lookup services (parsing, file writes such as the flag cache);
- tampering with the autostart registry value or the update/uninstall path.

Out of scope: the accuracy of third-party lookup services, and attacks that require an already compromised user account.

## Verifying downloads

Release assets are built by the GitHub Actions workflow in this repository and published with a SHA-256 checksum file.
The installer is not code-signed; compare the checksum with the one on the release page before running it.
