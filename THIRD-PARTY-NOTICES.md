# Third-party notices

InternetIPTaskbar itself is released under the [MIT License](LICENSE). It builds on, bundles, or talks to the following.

## Bundled in release builds

| Component | Use | License |
|---|---|---|
| [.NET 8 runtime and Windows Forms](https://github.com/dotnet/runtime) | Bundled in the self-contained executable | MIT |
| Country flag images from [flagcdn.com](https://flagcdn.com) | Embedded by `scripts/fetch-flags.ps1` for release builds (not stored in this repository) | Provided by flagcdn.com - see their terms. Flags are not copyrightable symbols in most jurisdictions, but check before redistributing the images on their own. |

## Used to build

| Component | Use | License |
|---|---|---|
| [Inno Setup](https://jrsoftware.org/isinfo.php) | Builds the installer; its runtime stub is part of the installer | Inno Setup License (free, redistribution of the generated installers is allowed) |
| [xUnit](https://xunit.net), Microsoft.NET.Test.Sdk | Unit tests (not shipped) | Apache-2.0 / MIT |

## Online services contacted at run time

The program does not bundle these; it sends plain HTTPS GET requests (no identifiers, no cookies) and is not affiliated with them:

| Service | Purpose |
|---|---|
| api.ipify.org, api6.ipify.org | Public IPv4 / IPv6 address |
| ipv4.icanhazip.com | Public IPv4 address |
| checkip.amazonaws.com | Public IPv4 address |
| ipinfo.io, ipwho.is, ipapi.co | Country, ISP and city for an IP |
| flagcdn.com | Flag images when they are not embedded |

Please respect each service's usage limits and terms. The default 30-second interval and the "ask country only when the IP changes" design keep the load low.
