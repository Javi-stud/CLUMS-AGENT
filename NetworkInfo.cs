using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace LabMonitorAgent;

public class NetworkInfo
{
    // Adapters created by VMs, VPNs and containers; never the PC's real LAN connection.
    private static readonly string[] VirtualMarkers =
    {
        "virtual", "vmware", "virtualbox", "hyper-v", "vethernet", "vpn", "tap-", "tunnel",
        "loopback", "bluetooth", "docker", "wsl", "pseudo"
    };

    private record Adapter(string Ip, string Mac);

    /// <summary>IPv4 address of the main network connection, or "" if there is none.</summary>
    public string GetIpAddress() => FindMainAdapter()?.Ip ?? "";

    /// <summary>MAC address (AA-BB-CC-DD-EE-FF) of the main network connection, or "" if there is none.</summary>
    public string GetMacAddress() => FindMainAdapter()?.Mac ?? "";

    private static Adapter? FindMainAdapter()
    {
        // Wired first, then Wi-Fi; within each, adapters that have a default gateway first.
        IEnumerable<NetworkInterface> candidates = NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up && IsWiredOrWifi(n.NetworkInterfaceType) && !IsVirtual(n))
            .OrderBy(n => n.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ? 1 : 0)
            .ThenBy(n => HasGateway(n) ? 0 : 1);

        foreach (NetworkInterface network in candidates)
        {
            string? ip = GetIPv4(network);
            byte[] mac = network.GetPhysicalAddress().GetAddressBytes();
            if (ip is not null && mac.Length > 0)
                return new Adapter(ip, BitConverter.ToString(mac));
        }

        return null;
    }

    private static bool IsWiredOrWifi(NetworkInterfaceType type) =>
        type is NetworkInterfaceType.Ethernet
            or NetworkInterfaceType.GigabitEthernet
            or NetworkInterfaceType.FastEthernetT
            or NetworkInterfaceType.FastEthernetFx
            or NetworkInterfaceType.Wireless80211;

    private static bool IsVirtual(NetworkInterface n)
    {
        string text = (n.Name + " " + n.Description).ToLowerInvariant();
        return VirtualMarkers.Any(text.Contains);
    }

    private static bool HasGateway(NetworkInterface n) =>
        n.GetIPProperties().GatewayAddresses.Any(g => g.Address.AddressFamily == AddressFamily.InterNetwork
                                                      && !g.Address.Equals(System.Net.IPAddress.Any));

    private static string? GetIPv4(NetworkInterface n)
    {
        foreach (UnicastIPAddressInformation address in n.GetIPProperties().UnicastAddresses)
        {
            if (address.Address.AddressFamily != AddressFamily.InterNetwork) continue;

            byte[] b = address.Address.GetAddressBytes();
            if (b[0] == 169 && b[1] == 254) continue; // 169.254.x.x = Windows gave up on DHCP

            return address.Address.ToString();
        }
        return null;
    }
}