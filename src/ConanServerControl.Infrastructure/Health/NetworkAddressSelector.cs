using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace ConanServerControl.Infrastructure.Health;

/// <summary>One network adapter, reduced to what address selection needs.</summary>
public sealed record NetworkAdapterSnapshot(
    string Name,
    string Description,
    NetworkInterfaceType Type,
    OperationalStatus Status,
    IReadOnlyList<IPAddress> IPv4Addresses,
    bool HasIPv4Gateway);

/// <summary>
/// Picks the physical LAN address and the Radmin VPN address separately. The deployment target is a
/// private friends-only server over Radmin VPN, so friends use the Radmin address; the physical LAN
/// address still serves players on the same real LAN. Other VPN, tunnel and virtual adapters are
/// never reported as either.
/// </summary>
public static class NetworkAddressSelector
{
    // Driver-provided description of the Radmin VPN adapter: "Famatech Radmin VPN Ethernet Adapter".
    // The adapter *name* is user-editable, so it alone never identifies Radmin.
    private static readonly string[] RadminDescriptionMarkers = ["Radmin VPN", "Famatech"];

    private static readonly string[] VirtualOrVpnMarkers =
    [
        "radmin", "famatech", "vpn", "virtual", "tailscale", "zerotier", "hamachi", "logmein", "wireguard",
        "wintun", "openvpn", "nordlynx", "tap-windows", "tap-win32", "tunnel", "pseudo", "loopback", "hyper-v",
        "vethernet", "vmware", "virtualbox", "docker", "wsl", "npcap", "bluetooth"
    ];

    private static readonly NetworkInterfaceType[] PhysicalTypes =
    [
        NetworkInterfaceType.Ethernet,
        NetworkInterfaceType.GigabitEthernet,
        NetworkInterfaceType.FastEthernetT,
        NetworkInterfaceType.FastEthernetFx,
        NetworkInterfaceType.Ethernet3Megabit,
        NetworkInterfaceType.Wireless80211
    ];

    public static string? SelectRadminIPv4(IEnumerable<NetworkAdapterSnapshot> adapters)
    {
        var candidates = adapters
            .Where(a => a.Status == OperationalStatus.Up && IsRadmin(a))
            .SelectMany(a => a.IPv4Addresses)
            .Where(IsUsableUnicast)
            .ToList();

        // Radmin VPN hands out 26.0.0.0/8 addresses; prefer one if the adapter has several.
        var preferred = candidates.FirstOrDefault(a => a.GetAddressBytes()[0] == 26) ?? candidates.FirstOrDefault();
        return preferred?.ToString();
    }

    public static string? SelectPhysicalLanIPv4(IEnumerable<NetworkAdapterSnapshot> adapters)
    {
        var physical = adapters
            .Where(a => a.Status == OperationalStatus.Up && PhysicalTypes.Contains(a.Type) && !IsVirtualOrVpn(a))
            .OrderByDescending(a => a.HasIPv4Gateway)
            .ToList();

        foreach (var adapter in physical)
        {
            var address = adapter.IPv4Addresses.FirstOrDefault(a => IsUsableUnicast(a) && IsPrivate(a));
            if (address is not null)
            {
                return address.ToString();
            }
        }

        return null;
    }

    internal static bool IsRadmin(NetworkAdapterSnapshot adapter) =>
        RadminDescriptionMarkers.Any(m => adapter.Description.Contains(m, StringComparison.OrdinalIgnoreCase));

    internal static bool IsVirtualOrVpn(NetworkAdapterSnapshot adapter)
    {
        var text = adapter.Name + " " + adapter.Description;
        return IsRadmin(adapter) ||
               adapter.Type is NetworkInterfaceType.Tunnel or NetworkInterfaceType.Ppp or NetworkInterfaceType.Loopback ||
               VirtualOrVpnMarkers.Any(m => text.Contains(m, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsUsableUnicast(IPAddress address)
    {
        if (address.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(address))
        {
            return false;
        }

        var b = address.GetAddressBytes();
        return !(b[0] == 169 && b[1] == 254) && b[0] != 0; // no APIPA (adapter without a lease)
    }

    private static bool IsPrivate(IPAddress address)
    {
        var b = address.GetAddressBytes();
        return b[0] == 10 || (b[0] == 172 && b[1] is >= 16 and <= 31) || (b[0] == 192 && b[1] == 168);
    }
}
