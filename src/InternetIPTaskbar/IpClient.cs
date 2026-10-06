using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text.Json;

namespace InternetIPTaskbar;

internal sealed record GeoInfo(string Country, string? Isp, string? City);

/// <summary>Public IP and geolocation lookup. Stateless apart from the geolocation cache.</summary>
internal static class IpClient
{
    // One shared client; the Connection: close header forces a new connection for every request,
    // so that after the VPN is switched on or off the request already takes the new route.
    static readonly HttpClient Http = new(new SocketsHttpHandler
    {
        UseCookies = false,
        AllowAutoRedirect = true,
        ConnectTimeout = TimeSpan.FromSeconds(3)
    })
    { Timeout = TimeSpan.FromSeconds(4) };

    static readonly (string Name, string Url)[] V4Services =
    [
        ("ipify", "https://api.ipify.org?format=text"),
        ("icanhazip", "https://ipv4.icanhazip.com"),
        ("amazon", "https://checkip.amazonaws.com")
    ];

    static readonly ConcurrentDictionary<string, GeoInfo> GeoCache = new();

    /// <summary>
    /// Queries two services; a third is used only if they disagree or one did not answer.
    /// The result is the IP with the most votes (ties are resolved in favour of ipify).
    /// </summary>
    public static async Task<string?> GetIpv4Async(CancellationToken ct)
    {
        var results = new List<(string Name, string? Ip)>(
            await Task.WhenAll(V4Services.Take(2).Select(s => QueryV4(s, ct))));

        if (results[0].Ip is null || results[0].Ip != results[1].Ip)
            results.Add(await QueryV4(V4Services[2], ct));

        var good = results.Where(r => r.Ip is not null).ToList();
        if (good.Count == 0) return null;

        if (good.Select(r => r.Ip).Distinct().Count() > 1)
            Log.Info("IP services disagree: " + string.Join(", ", results.Select(r => $"{r.Name}={r.Ip ?? "-"}")));

        return good
            .GroupBy(r => r.Ip!)
            .OrderByDescending(g => g.Count())
            .ThenByDescending(g => g.Any(r => r.Name == "ipify"))
            .First().Key;
    }

    static async Task<(string Name, string? Ip)> QueryV4((string Name, string Url) service, CancellationToken ct)
    {
        var text = await GetTextAsync(service.Url, ct);
        var ip = IPAddress.TryParse(text, out var a) && a.AddressFamily == AddressFamily.InterNetwork ? text : null;
        Log.Debug($"IPv4 {service.Name} = {ip ?? "invalid"}");
        return (service.Name, ip);
    }

    /// <summary>The public IPv6 address, or null when there is no IPv6 connectivity.</summary>
    public static async Task<string?> GetIpv6Async(CancellationToken ct)
    {
        var text = await GetTextAsync("https://api6.ipify.org", ct);
        var ip = IPAddress.TryParse(text, out var a) && a.AddressFamily == AddressFamily.InterNetworkV6 ? a.ToString() : null;
        Log.Debug($"IPv6 = {ip ?? "none"}");
        return ip;
    }

    /// <summary>Country, ISP and city for an IP. Results are cached for the lifetime of the process.</summary>
    public static async Task<GeoInfo?> GetGeoAsync(string ip, CancellationToken ct)
    {
        if (GeoCache.TryGetValue(ip, out var cached)) return cached;

        foreach (var (name, lookup) in GeoProviders)
        {
            try
            {
                var geo = await lookup(ip, ct);
                if (geo is null) { Log.Debug($"GEO {name}: no data for {ip}"); continue; }
                if (GeoCache.Count > 64) GeoCache.Clear();
                GeoCache[ip] = geo;
                Log.Debug($"GEO {name}: {ip} -> {geo.Country} {geo.Isp} {geo.City}");
                return geo;
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                Log.Debug($"GEO {name} failed: {ex.GetType().Name}: {ex.Message}");
            }
        }
        Log.Info($"GEO lookup failed for {ip}");
        return null;
    }

    static readonly (string Name, Func<string, CancellationToken, Task<GeoInfo?>> Lookup)[] GeoProviders =
    [
        ("ipinfo", async (ip, ct) =>
        {
            var text = await GetTextAsync($"https://ipinfo.io/{ip}/json", ct);
            if (text is null) return null;
            using var doc = JsonDocument.Parse(text);
            var r = doc.RootElement;
            return MakeGeo(Str(r, "country"), Str(r, "org"), Str(r, "city"));
        }),
        ("ipwho.is", async (ip, ct) =>
        {
            var text = await GetTextAsync($"https://ipwho.is/{ip}", ct);
            if (text is null) return null;
            using var doc = JsonDocument.Parse(text);
            var r = doc.RootElement;
            if (!(r.TryGetProperty("success", out var ok) && ok.ValueKind == JsonValueKind.True)) return null;
            string? isp = r.TryGetProperty("connection", out var c) ? Str(c, "isp") : null;
            return MakeGeo(Str(r, "country_code"), isp, Str(r, "city"));
        }),
        ("ipapi.co", async (ip, ct) =>
        {
            var text = await GetTextAsync($"https://ipapi.co/{ip}/json/", ct);
            if (text is null) return null;
            using var doc = JsonDocument.Parse(text);
            var r = doc.RootElement;
            return MakeGeo(Str(r, "country_code"), Str(r, "org"), Str(r, "city"));
        })
    ];

    static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    static GeoInfo? MakeGeo(string? country, string? isp, string? city)
    {
        country = country?.Trim().ToUpperInvariant();
        if (country is not { Length: 2 } || !country.All(char.IsAsciiLetter)) return null;
        return new GeoInfo(country, Clean(isp), Clean(city));
    }

    static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    /// <summary>GET with caching disabled. Returns null on a network error or a non-2xx status; cancellation is propagated.</summary>
    static async Task<string?> GetTextAsync(string url, CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.ConnectionClose = true;
            request.Headers.CacheControl = new() { NoCache = true, NoStore = true };
            request.Headers.UserAgent.ParseAdd(AppInfo.UserAgent);
            using var response = await Http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                Log.Debug($"HTTP {(int)response.StatusCode} {new Uri(url).Host}");
                return null;
            }
            return (await response.Content.ReadAsStringAsync(ct)).Trim();
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            Log.Debug($"HTTP fail {new Uri(url).Host}: {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    /// <summary>Downloads binary data (flags). Throws on failure.</summary>
    public static async Task<byte[]> GetBytesAsync(string url, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd(AppInfo.UserAgent);
        using var response = await Http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync(ct);
    }
}
