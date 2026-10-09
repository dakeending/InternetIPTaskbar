using System.Diagnostics;
using System.Net.NetworkInformation;

namespace InternetIPTaskbar;

internal enum NetState { Starting, Ok, Stale, Offline }

internal sealed record Snapshot(
    string? Ip,
    GeoInfo? Geo,
    string? Ipv6,
    GeoInfo? Ipv6Geo,
    NetState State,
    bool Alert,
    bool Ipv6Mismatch,
    DateTime? Updated)
{
    public static readonly Snapshot Empty = new(null, null, null, null, NetState.Starting, false, false, null);

    public string? Country => Geo?.Country;
}

/// <summary>
/// Background check loop. The only writer of its fields is the LoopAsync task;
/// immutable Snapshot objects are published to the outside.
/// </summary>
internal sealed class IpMonitor : IDisposable
{
    readonly Settings settings;
    readonly CancellationTokenSource cts = new();
    readonly SemaphoreSlim wake = new(0, 1);

    string? ip, ipv6;
    GeoInfo? geo, ipv6Geo;
    int failures;
    bool firstRun = true;
    DateTime lastV6 = DateTime.MinValue, geoRetryAt = DateTime.MinValue;
    DateTime? updated;
    int netGeneration;
    string wakeReason = "wake";

    public event Action<Snapshot>? Changed;
    public Snapshot Current { get; private set; } = Snapshot.Empty;

    public IpMonitor(Settings settings) => this.settings = settings;

    public void Start()
    {
        NetworkChange.NetworkAddressChanged += OnNetworkChanged;
        NetworkChange.NetworkAvailabilityChanged += OnNetworkChanged;
        _ = Task.Run(LoopAsync);
    }

    /// <summary>Requests an immediate check.</summary>
    public void CheckNow(string reason = "manual")
    {
        wakeReason = reason;
        if (wake.CurrentCount == 0)
        {
            try { wake.Release(); } catch (SemaphoreFullException) { }
        }
    }

    /// <summary>Recomputes the alert after a settings change without touching the network.</summary>
    public void Reevaluate() => Raise(Current);

    void OnNetworkChanged(object? sender, EventArgs e)
    {
        // Network events arrive in bursts and a VPN route is not established instantly:
        // one check after 2 s and a follow-up check after 8 s.
        int gen = Interlocked.Increment(ref netGeneration);
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(2000, cts.Token);
                if (gen != netGeneration) return;
                CheckNow("net-change");
                await Task.Delay(6000, cts.Token);
                if (gen != netGeneration) return;
                CheckNow("net-change-followup");
            }
            catch (OperationCanceledException) { }
        });
    }

    async Task LoopAsync()
    {
        string reason = "start";
        while (!cts.IsCancellationRequested)
        {
            var started = Stopwatch.GetTimestamp();
            try { await CheckAsync(reason, cts.Token); }
            catch (OperationCanceledException) when (cts.IsCancellationRequested) { break; }
            catch (Exception ex) { Log.Info($"CHECK error {ex.GetType().Name}: {ex.Message}"); }

            // retry more often after a failure so we recover quickly once the network is back
            int interval = failures > 0 ? Math.Min(settings.IntervalSeconds, 5) : settings.IntervalSeconds;
            var wait = TimeSpan.FromSeconds(interval) - Stopwatch.GetElapsedTime(started);
            if (wait < TimeSpan.FromSeconds(1)) wait = TimeSpan.FromSeconds(1);

            try { reason = await wake.WaitAsync(wait, cts.Token) ? wakeReason : "scheduled"; }
            catch (OperationCanceledException) { break; }
        }
    }

    async Task CheckAsync(string reason, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var newIp = await IpClient.GetIpv4Async(ct);

        if (newIp is null)
        {
            failures++;
            bool offline = failures >= 3 || !NetworkInterface.GetIsNetworkAvailable();
            Log.Debug($"CHECK ({reason}) no IP, failures={failures}");
            if (failures == 1 || offline && failures == 3) Log.Info($"CHECK failed ({reason}), failures={failures}, offline={offline}");
            Publish(offline ? NetState.Offline : NetState.Stale);
            return;
        }

        if (failures > 0) Log.Info($"CHECK recovered after {failures} failures");
        failures = 0;
        updated = DateTime.Now;

        bool changed = newIp != ip;
        if (changed)
        {
            Log.Info($"IP {ip ?? "-"} -> {newIp} ({reason})");
            ip = newIp;
            geo = null;
        }

        if (geo is null && (changed || DateTime.UtcNow >= geoRetryAt))
        {
            geo = await IpClient.GetGeoAsync(newIp, ct);
            if (geo is null) geoRetryAt = DateTime.UtcNow.AddSeconds(60);
            else Log.Info($"GEO {newIp}: {geo.Country} | {geo.Isp ?? "-"} | {geo.City ?? "-"}");
        }

        // show the IPv4 result right away: an IPv6 check without IPv6 connectivity waits for a timeout
        Publish(NetState.Ok);

        if (settings.CheckIpv6)
        {
            bool due = changed || firstRun || reason.StartsWith("net") || DateTime.UtcNow - lastV6 > TimeSpan.FromMinutes(10);
            if (due)
            {
                lastV6 = DateTime.UtcNow;
                var v6 = await IpClient.GetIpv6Async(ct);
                if (v6 != ipv6)
                {
                    Log.Info($"IPv6 {ipv6 ?? "-"} -> {v6 ?? "-"}");
                    ipv6 = v6;
                    ipv6Geo = null;
                }
                if (ipv6 is not null && ipv6Geo is null) ipv6Geo = await IpClient.GetGeoAsync(ipv6, ct);
                Publish(NetState.Ok);
            }
        }
        else if (ipv6 is not null) { ipv6 = null; ipv6Geo = null; Publish(NetState.Ok); }

        firstRun = false;
        Log.Debug($"CHECK ({reason}) {newIp} done in {sw.ElapsedMilliseconds} ms");
    }

    void Publish(NetState state)
    {
        bool mismatch = ipv6Geo is not null && geo is not null && ipv6Geo.Country != geo.Country;
        Raise(new Snapshot(ip, geo, ipv6, ipv6Geo, state, false, mismatch, updated));
    }

    void Raise(Snapshot s)
    {
        s = s with { Alert = ComputeAlert(s) };
        Current = s;
        Changed?.Invoke(s);
    }

    bool ComputeAlert(Snapshot s)
    {
        if (s.State is not (NetState.Ok or NetState.Stale) || s.Ip is null) return false;
        return settings.AlertMode switch
        {
            AlertMode.ByIp => !string.IsNullOrEmpty(settings.HomeIp) && s.Ip == settings.HomeIp,
            AlertMode.ByCountry => !string.IsNullOrEmpty(settings.HomeCountry) && s.Country == settings.HomeCountry,
            AlertMode.ByRange => IpRange.Contains(settings.HomeRange, s.Ip),
            _ => false
        };
    }

    public void Dispose()
    {
        NetworkChange.NetworkAddressChanged -= OnNetworkChanged;
        NetworkChange.NetworkAvailabilityChanged -= OnNetworkChanged;
        cts.Cancel();
    }
}

