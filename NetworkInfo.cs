using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace LabMonitorAgent;

public class NetworkInfo
{
    public string GetEthernetIPAddress()
    {
        foreach (NetworkInterface network in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (network.NetworkInterfaceType == NetworkInterfaceType.Ethernet &&
                network.OperationalStatus == OperationalStatus.Up)
            {
                foreach (UnicastIPAddressInformation address in network.GetIPProperties().UnicastAddresses)
                {
                    if (address.Address.AddressFamily == AddressFamily.InterNetwork)
                    {
                        return address.Address.ToString();
                    }
                }
            }
        }

        return "No active Ethernet IPv4 address found";
    }

    public string GetEthernetMacAddress()
{
    foreach (NetworkInterface network in NetworkInterface.GetAllNetworkInterfaces())
    {
        if (network.NetworkInterfaceType == NetworkInterfaceType.Ethernet &&
            network.OperationalStatus == OperationalStatus.Up)
        {
            return BitConverter.ToString(
                network.GetPhysicalAddress().GetAddressBytes()
            );
        }
    }

    return "No active Ethernet MAC address found";
}
}