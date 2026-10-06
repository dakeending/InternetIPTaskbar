using System.Text.Json;
using System.Text.Json.Serialization;

namespace InternetIPTaskbar;

internal enum Placement { NearTray, Center }

internal enum AlertMode { Off, ByIp, ByCountry }

internal sealed class Settings
{
    public static readonly int[] Intervals = [1, 3, 5, 30, 60];

    public int IntervalSeconds { get; set; } = 30;
    public Placement Placement { get; set; } = Placement.NearTray;
    public AlertMode AlertMode { get; set; } = AlertMode.Off;
    public string? HomeIp { get; set; }
    public string? HomeCountry { get; set; }
    public bool AlertSound { get; set; }
    public bool CheckIpv6 { get; set; } = true;
    public bool DebugLog { get; set; }

    static readonly string FilePath = Path.Combine(AppInfo.DataDir, "settings.json");
    static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static Settings Load()
    {
        Settings s;
        try
        {
            s = File.Exists(FilePath)
                ? JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath), Json) ?? new Settings()
                : new Settings();
        }
        catch (Exception ex)
        {
            Log.Info($"SETTINGS load failed, defaults used: {ex.GetType().Name}: {ex.Message}");
            s = new Settings();
        }
        if (!Intervals.Contains(s.IntervalSeconds)) s.IntervalSeconds = 30;
        return s;
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(AppInfo.DataDir);
            var tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(this, Json));
            File.Move(tmp, FilePath, true);
        }
        catch (Exception ex) { Log.Info($"SETTINGS save failed: {ex.GetType().Name}: {ex.Message}"); }
    }
}
