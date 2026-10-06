using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using InternetIPTaskbar;
using Xunit;

namespace InternetIPTaskbar.Tests;

public class HornSoundTests
{
    static byte[] Wav() => HornSound.BuildWav();

    [Fact]
    public void Produces_valid_riff_wave_header()
    {
        var wav = Wav();
        Assert.Equal("RIFF", Encoding.ASCII.GetString(wav, 0, 4));
        Assert.Equal("WAVE", Encoding.ASCII.GetString(wav, 8, 4));
        Assert.Equal("data", Encoding.ASCII.GetString(wav, 36, 4));
        // the declared data length matches the real one
        Assert.Equal(wav.Length - 44, BitConverter.ToInt32(wav, 40));
    }

    [Fact]
    public void Lasts_about_one_second()
    {
        var wav = Wav();
        int rate = BitConverter.ToInt32(wav, 24);
        double seconds = (wav.Length - 44) / 2.0 / rate;
        Assert.InRange(seconds, 0.9, 1.2);
    }

    [Fact]
    public void Contains_two_blasts_separated_by_silence()
    {
        var wav = Wav();
        int rate = BitConverter.ToInt32(wav, 24);
        int samples = (wav.Length - 44) / 2;
        var pcm = new short[samples];
        Buffer.BlockCopy(wav, 44, pcm, 0, samples * 2);

        // 50 ms windows: loud (L) or quiet (.)
        int win = rate / 20;
        var pattern = new StringBuilder();
        for (int i = 0; i + win <= samples; i += win)
        {
            double rms = Math.Sqrt(pcm.Skip(i).Take(win).Sum(s => (double)s * s) / win);
            pattern.Append(rms > 1500 ? 'L' : '.');
        }
        // loud ... quiet ... loud
        Assert.Matches(new Regex("^L{6,}\\.+L{6,}"), pattern.ToString().TrimEnd('.'));
    }
}

public class SettingsTests
{
    [Fact]
    public void Intervals_are_ascending_and_include_one_second()
    {
        Assert.Equal(new[] { 1, 3, 5, 30, 60 }, Settings.Intervals);
    }

    [Fact]
    public void Defaults_are_safe()
    {
        var s = new Settings();
        Assert.Contains(s.IntervalSeconds, Settings.Intervals);
        Assert.Equal(AlertMode.Off, s.AlertMode);
        Assert.False(s.AlertSound);       // the horn is opt-in
        Assert.Equal(Placement.NearTray, s.Placement);
    }
}

public class AppInfoTests
{
    [Fact]
    public void Version_is_semver_like() => Assert.Matches(@"^\d+\.\d+\.\d+$", AppInfo.Version);

    [Fact]
    public void User_agent_carries_the_version() => Assert.Equal($"InternetIPTaskbar/{AppInfo.Version}", AppInfo.UserAgent);
}

public class StringsTests
{
    [Fact]
    public void Every_string_property_is_non_empty()
    {
        var props = typeof(Strings).GetProperties(BindingFlags.Public | BindingFlags.Static)
            .Where(p => p.PropertyType == typeof(string)).ToList();
        Assert.NotEmpty(props);
        foreach (var p in props)
            Assert.False(string.IsNullOrWhiteSpace((string?)p.GetValue(null)), p.Name);
    }

    [Fact]
    public void Formatted_strings_include_their_arguments()
    {
        Assert.Contains("5", Strings.Seconds(5));
        Assert.Contains("1.2.3", Strings.Exit("1.2.3"));
        Assert.Contains("203.0.113.7", Strings.AlertBody(true, "203.0.113.7", "NL"));
    }
}
