using System.Net;
using System.Net.Sockets;

namespace Roadmap.Api;

/// <summary>
/// SSRF guard for the "fetch this URL for me" tools (article images, meal photos): resolve the
/// host and refuse if any resolved address is loopback, private, link-local (including the
/// 169.254.169.254 cloud-metadata address), CGNAT, or IPv6 unique-local.
/// </summary>
public static class UrlGuard
{
    public static async Task<bool> IsBlockedHostAsync(Uri uri)
    {
        IPAddress[] addrs;
        if (IPAddress.TryParse(uri.Host, out var literal)) addrs = new[] { literal };
        else
        {
            try { addrs = await Dns.GetHostAddressesAsync(uri.Host); }
            catch { return true; }
        }
        return addrs.Length == 0 || addrs.Any(IsPrivateAddress);
    }

    private static bool IsPrivateAddress(IPAddress ip)
    {
        if (IPAddress.IsLoopback(ip)) return true;
        if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
        var b = ip.GetAddressBytes();
        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            return b[0] == 0                                   // 0.0.0.0/8
                || b[0] == 10                                  // 10.0.0.0/8
                || (b[0] == 100 && b[1] >= 64 && b[1] <= 127)  // 100.64.0.0/10 (CGNAT)
                || (b[0] == 169 && b[1] == 254)                // 169.254.0.0/16 (link-local + metadata)
                || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)   // 172.16.0.0/12
                || (b[0] == 192 && b[1] == 168);               // 192.168.0.0/16
        }
        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
            return ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || (b[0] & 0xFE) == 0xFC; // + fc00::/7 unique-local
        return true;
    }
}
