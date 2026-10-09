using System.Net;
using System.Net.Sockets;

namespace InternetIPTaskbar;

/// <summary>IPv4 ranges in CIDR notation ("203.0.113.0/24"): building one from an address and testing membership.</summary>
internal static class IpRange
{
    /// <summary>The prefix length used when a range is derived from the remembered real IP.</summary>
    public const int DefaultPrefix = 24;

    /// <summary>The network that contains <paramref name="ip"/>, e.g. 203.0.113.42 -> "203.0.113.0/24". Null for anything but a valid IPv4 address.</summary>
    public static string? FromIp(string? ip, int prefix = DefaultPrefix)
    {
        if (prefix is < 0 or > 32 || !TryParseIpv4(ip, out uint value)) return null;
        return $"{ToString(value & Mask(prefix))}/{prefix}";
    }

    /// <summary>True when <paramref name="ip"/> lies inside <paramref name="cidr"/>. Invalid input never matches.</summary>
    public static bool Contains(string? cidr, string? ip)
    {
        if (string.IsNullOrWhiteSpace(cidr) || !TryParseIpv4(ip, out uint address)) return false;
        var parts = cidr.Split('/');
        if (parts.Length != 2 || !int.TryParse(parts[1], out int prefix) || prefix is < 0 or > 32) return false;
        if (!TryParseIpv4(parts[0], out uint network)) return false;
        uint mask = Mask(prefix);
        return (address & mask) == (network & mask);
    }

    static uint Mask(int prefix) => prefix == 0 ? 0 : uint.MaxValue << (32 - prefix);

    static bool TryParseIpv4(string? text, out uint value)
    {
        value = 0;
        if (!IPAddress.TryParse(text, out var address) || address.AddressFamily != AddressFamily.InterNetwork) return false;
        var b = address.GetAddressBytes();
        value = (uint)(b[0] << 24 | b[1] << 16 | b[2] << 8 | b[3]);
        return true;
    }

    static string ToString(uint v) => $"{v >> 24}.{(v >> 16) & 255}.{(v >> 8) & 255}.{v & 255}";
}
