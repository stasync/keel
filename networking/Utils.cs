using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Keel.Networking
{
    public static class Utils
    {
        public static IPAddress GetIpAddressEthernet()
        {
            foreach (var network in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (network.NetworkInterfaceType != NetworkInterfaceType.Ethernet)
                    continue;

                var properties = network.GetIPProperties();
                foreach (var address in properties.UnicastAddresses)
                {
                    if (address.Address.AddressFamily != AddressFamily.InterNetwork && address.Address.AddressFamily != AddressFamily.InterNetworkV6)
                        continue;

                    if (IPAddress.IsLoopback(address.Address))
                        continue;

                    return address.Address;
                }
            }

            return GetIPv4Address();
        }

        public static IPAddress GetIPv4Address()
        {
            var entry = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ipAddress in entry.AddressList)
            {
                if (ipAddress.AddressFamily == AddressFamily.InterNetwork)
                    return ipAddress;
            }

            return GetIpAddress();
        }

        public static int GetAvailableUdpPort()
        {
            using var udpClient = new UdpClient(port: 0);
            var ipEndPoint = (IPEndPoint)udpClient.Client.LocalEndPoint;
            return ipEndPoint?.Port ?? 0;
        }

        public static int GetAvailableTcpPort()
        {
            var tcpListener = new TcpListener(localaddr: IPAddress.Any, port: 0);
            try
            {
                tcpListener.Start();
                var ipEndPoint = (IPEndPoint)tcpListener.LocalEndpoint;
                return ipEndPoint.Port;
            }
            finally
            {
                tcpListener.Stop();
            }
        }

        public static bool IsUdpPortAvailable(int port)
        {
            // A port number must be between 1 and 65535.
            if (port is < 1 or > ushort.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(port), $"Port number must be between 1 and {ushort.MaxValue}.");

            try
            {
                using var udpClient = new UdpClient(port);
                var ipEndPoint = (IPEndPoint)udpClient.Client.LocalEndPoint;
                return ipEndPoint?.Port == port;
            }
            catch
            {
                // Catch all other exceptions here (NOTE: Maybe we should catch only SocketException here?).
                // Error code 10048 (WSAEADDRINUSE) typically means the address (port) is already in use.
                // It's safer to generally return false or rethrow depending on desired strictness.
                // For simplicity, we'll assume any failure to listen means the port is NOT available.
                return false;
            }
        }

        public static bool CanStartHttpListener(string prefix)
        {
            if (string.IsNullOrWhiteSpace(prefix))
                throw new ArgumentException("Address cannot be null or empty.", nameof(prefix));

            try
            {
                using var httpListener = new HttpListener();
                httpListener.Prefixes.Add(uriPrefix: prefix);
                httpListener.Start();
                httpListener.Stop();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static IPAddress GetIpAddress()
        {
            var entry = Dns.GetHostEntry(Dns.GetHostName());
            return entry.AddressList.Length > 0 ? entry.AddressList[0] : null;
        }

        public static IPAddress GetIpAddressFromEndPoint(EndPoint endPoint)
        {
            var ipEndPoint = endPoint as IPEndPoint;
            return ipEndPoint?.Address;
        }

        public static string GetHostName() =>
            Dns.GetHostName();

        public static string CombineUrl(params string[] urlParts)
        {
            if (urlParts == null || urlParts.Length == 0)
                throw new ArgumentException("URL parts cannot be empty.");

            if (!IsValidUrl(urlParts[0]))
                throw new InvalidOperationException($"Url leading part '{urlParts[0]}' is not a valid URL.");

            for (var i = 0; i < urlParts.Length; i++)
            {
                if (i == 0)
                    urlParts[i] = urlParts[i].TrimEnd('/');
                else if (i == urlParts.Length - 1)
                    urlParts[i] = urlParts[i].TrimStart('/');
                else
                    urlParts[i] = urlParts[i].Trim('/');
            }

            var result = string.Join("/", urlParts);
            if (!result.EndsWith('/'))
                result += '/';

            if (!IsValidUrl(result))
                throw new InvalidOperationException($"Result string '{result}' is not a valid URL.");

            return result;
        }

        public static bool IsValidUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uriResult))
                return false;

            if (uriResult.Scheme != Uri.UriSchemeHttp && uriResult.Scheme != Uri.UriSchemeHttps)
                return false;

            return uriResult.AbsoluteUri.EndsWith('/');
        }
    }
}