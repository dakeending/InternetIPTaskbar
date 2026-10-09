# Changelog

All notable changes to this project are documented here.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [2.1.1] - 2026-10-09

### Changed
- VPN guard: three independent choices - real IP, real IP range (/24) or real country. Each "Remember current ..." item stores only its own value and switches the alert to that mode; the remembered values are shown next to the modes.

## [2.1.0] - 2026-10-09

### Added
- Traditional Chinese (Hong Kong) user interface and a language switch in *Settings -> Language* (Auto / English / Russian / Traditional Chinese).
- VPN guard mode "real IP range": the /24 network of the remembered real IP is stored and watched in addition to the real IP and the real country.

### Changed
- Plate colours: green when everything is fine, grey while there is no network or the IP is being detected, bright orange on an alert.
- The alert plate blinks at 3 Hz for 10 seconds and then stays steady orange while the alert lasts.
- A thin white outline around the flag (window and tray icon).
- The plate no longer follows the Windows light/dark theme.

## [2.0.0] - 2026-10-06

First public release. A rewrite of the earlier private 1.x prototype.

### Added
- "Flag + IP" window next to the notification area (or centred); click opens the menu.
- Tray icon drawn from the flag, tooltip with IP, country and ISP; double-click copies the IP.
- VPN guard: remember the real IP or country and get a red window and a notification when the exit matches it; optional double steamship horn (off by default).
- IPv6 display and a leak warning when IPv6 comes from a different country than IPv4.
- Settings menu: check interval (1/3/5/30/60 s), position, VPN guard, horn, IPv6, start with Windows, verbose log, uninstall.
- Reaction to network-change events (checks 2 s and 8 s after a change).
- Per-monitor DPI, light/dark theme, taskbar on any edge, auto-hide taskbar, hiding in full-screen apps.
- English and Russian user interface.
- Installer (Inno Setup, per-user, English and Russian) with an optional autostart task and an uninstall shortcut.
- Unit tests and a GitHub Actions CI / release workflow.

### Changed
- IPv4-only lookup sources; country and ISP are queried only when the IP changes.
- A single shared HTTP client with `Connection: close`, so requests follow a new VPN route.
- Log rotation (3 x 1 MB); only IP changes and errors are logged unless verbose logging is on.
- Settings are persisted in `settings.json`; the version is defined in a single place.
- Published as one compressed self-contained executable.

### Fixed
- A stale flag could stay on screen after the country changed, and a late flag download could override the new one.
- A corrupt flag cache file broke that country's flag permanently.
- "No network" is shown instead of an outdated IP when every lookup fails.
- The tray menu no longer crashes while it is rebuilt, and the IP window no longer covers an open menu.

[Unreleased]: https://github.com/dakeending/InternetIPTaskbar/compare/v2.1.1...HEAD
[2.1.1]: https://github.com/dakeending/InternetIPTaskbar/compare/v2.1.0...v2.1.1
[2.1.0]: https://github.com/dakeending/InternetIPTaskbar/compare/v2.0.0...v2.1.0
[2.0.0]: https://github.com/dakeending/InternetIPTaskbar/releases/tag/v2.0.0
