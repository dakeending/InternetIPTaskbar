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
    static readonly UiLanguage[] Languages = [UiLanguage.English, UiLanguage.Russian, UiLanguage.ChineseHK];

    static List<PropertyInfo> StringProperties() => typeof(Strings)
        .GetProperties(BindingFlags.Public | BindingFlags.Static)
        .Where(p => p.PropertyType == typeof(string)).ToList();

    [Theory]
    [InlineData("English")]
    [InlineData("Russian")]
    [InlineData("ChineseHK")]
    public void Every_string_property_is_non_empty_in_every_language(string languageName)
    {
        var language = Enum.Parse<UiLanguage>(languageName); // the enum is internal, so a public test cannot take it as a parameter
        var old = Strings.Language;
        try
        {
            Strings.Language = language;
            var props = StringProperties();
            Assert.NotEmpty(props);
            foreach (var p in props)
                Assert.False(string.IsNullOrWhiteSpace((string?)p.GetValue(null)), $"{language}: {p.Name}");
        }
        finally { Strings.Language = old; }
    }

    [Fact]
    public void Chinese_uses_traditional_characters_and_russian_uses_cyrillic()
    {
        var old = Strings.Language;
        try
        {
            Strings.Language = UiLanguage.ChineseHK;
            Assert.Matches("[一-鿿]", Strings.Settings);
            Assert.Equal("設定", Strings.Settings);     // Traditional (a Simplified form would be 设置)
            Assert.Equal("結束（v1.0.0）", Strings.Exit("1.0.0"));

            Strings.Language = UiLanguage.Russian;
            Assert.Matches("[Ѐ-ӿ]", Strings.Settings);

            Strings.Language = UiLanguage.English;
            Assert.Equal("Settings", Strings.Settings);
        }
        finally { Strings.Language = old; }
    }

    [Fact]
    public void Languages_actually_differ()
    {
        var old = Strings.Language;
        try
        {
            var results = Languages.Select(l => { Strings.Language = l; return Strings.CheckNow; }).ToList();
            Assert.Equal(3, results.Distinct().Count());
        }
        finally { Strings.Language = old; }
    }

    [Fact]
    public void Formatted_strings_include_their_arguments_in_every_language()
    {
        var old = Strings.Language;
        try
        {
            foreach (var l in Languages)
            {
                Strings.Language = l;
                Assert.Contains("5", Strings.Seconds(5));
                Assert.Contains("1.2.3", Strings.Exit("1.2.3"));
                foreach (var mode in new[] { AlertMode.ByIp, AlertMode.ByCountry, AlertMode.ByRange })
                {
                    Assert.Contains("203.0.113.7", Strings.AlertBody(mode, "203.0.113.7", "NL"));
                    Assert.Contains("VALUE-42", Strings.HomeSavedBody(mode, "VALUE-42"));
                }
                // the three guard choices show their remembered value, or a "not set" text when empty
                Assert.Contains("203.0.113.7", Strings.GuardByIp("203.0.113.7"));
                Assert.Contains("203.0.113.0/24", Strings.GuardByRange("203.0.113.0/24"));
                Assert.Contains("NL", Strings.GuardByCountry("NL"));
                Assert.NotEqual(Strings.GuardByIp(null), Strings.GuardByRange(null));
                Assert.NotEqual(Strings.GuardByRange(null), Strings.GuardByCountry(null));
            }
        }
        finally { Strings.Language = old; }
    }
}

public class IpRangeTests
{
    [Theory]
    [InlineData("203.0.113.42", 24, "203.0.113.0/24")]
    [InlineData("203.0.113.42", 16, "203.0.0.0/16")]
    [InlineData("203.0.113.42", 32, "203.0.113.42/32")]
    [InlineData("203.0.113.42", 0, "0.0.0.0/0")]
    [InlineData("255.255.255.255", 24, "255.255.255.0/24")]
    public void Builds_the_network_from_an_address(string ip, int prefix, string expected)
        => Assert.Equal(expected, IpRange.FromIp(ip, prefix));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not an ip")]
    [InlineData("2001:db8::1")]
    [InlineData("300.1.1.1")]
    public void Rejects_anything_but_ipv4(string? ip) => Assert.Null(IpRange.FromIp(ip));

    [Fact]
    public void Default_prefix_is_24() => Assert.Equal("198.51.100.0/24", IpRange.FromIp("198.51.100.7"));

    [Theory]
    [InlineData("203.0.113.0/24", "203.0.113.1", true)]
    [InlineData("203.0.113.0/24", "203.0.113.255", true)]
    [InlineData("203.0.113.0/24", "203.0.114.1", false)]
    [InlineData("203.0.0.0/16", "203.0.200.9", true)]
    [InlineData("203.0.0.0/16", "203.1.0.1", false)]
    [InlineData("203.0.113.42/32", "203.0.113.42", true)]
    [InlineData("203.0.113.42/32", "203.0.113.43", false)]
    [InlineData("0.0.0.0/0", "8.8.8.8", true)]
    public void Tests_membership(string cidr, string ip, bool expected) => Assert.Equal(expected, IpRange.Contains(cidr, ip));

    [Theory]
    [InlineData(null, "1.2.3.4")]
    [InlineData("", "1.2.3.4")]
    [InlineData("1.2.3.0", "1.2.3.4")]        // no prefix
    [InlineData("1.2.3.0/33", "1.2.3.4")]     // prefix out of range
    [InlineData("1.2.3.0/x", "1.2.3.4")]
    [InlineData("1.2.3.0/24", null)]
    [InlineData("1.2.3.0/24", "2001:db8::1")] // IPv6 never matches an IPv4 range
    public void Invalid_input_never_matches(string? cidr, string? ip) => Assert.False(IpRange.Contains(cidr, ip));
}

public class BlinkTests
{
    [Fact]
    public void Blinks_three_times_per_second()
    {
        // 3 Hz = 3 full on/off cycles per second: the phase flips every 1/6 s and starts "on"
        Assert.True(OverlayForm.BlinkPhaseOn(0.00));
        Assert.True(OverlayForm.BlinkPhaseOn(0.16));
        Assert.False(OverlayForm.BlinkPhaseOn(0.17));
        Assert.False(OverlayForm.BlinkPhaseOn(0.32));
        Assert.True(OverlayForm.BlinkPhaseOn(0.34));

        int flips = 0;
        bool previous = OverlayForm.BlinkPhaseOn(0);
        for (double t = 0.001; t < 1.0; t += 0.001)
        {
            bool now = OverlayForm.BlinkPhaseOn(t);
            if (now != previous) flips++;
            previous = now;
        }
        Assert.Equal(5, flips); // within one second: 3 bright phases separated by 2 dark phases, ending dark
    }

    [Fact]
    public void Constants_match_the_specification()
    {
        Assert.Equal(3, OverlayForm.BlinkHz);
        Assert.Equal(10, OverlayForm.BlinkSeconds);
    }
}
