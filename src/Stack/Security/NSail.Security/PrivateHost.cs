// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Net;
using System.Net.Sockets;

namespace NSail.Security;

/// <summary>Whether a host a tenant typed into a setting points the server at its own network:
/// loopback, private, link-local, CGNAT, unspecified or multicast space, or a name only a local
/// resolver answers. Whether a caller lets such a host through is the caller's own option.</summary>
public static class PrivateHost
{
    /// <summary>True when the host is <c>localhost</c>, <c>*.localhost</c>, <c>*.local</c>,
    /// <c>*.internal</c> or a single-label name, a literal private address (brackets allowed),
    /// or a name any of whose addresses is private. A name that does not resolve now is not
    /// refused for it.</summary>
    public static async Task<bool> Is(string host, CancellationToken cancellationToken = default)
    {
        host = host.Trim().Trim('[', ']').TrimEnd('.');

        if (host.Length == 0)
        {
            return false;
        }

        if (IPAddress.TryParse(host, out var literal))
        {
            return Is(literal);
        }

        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".local", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".internal", StringComparison.OrdinalIgnoreCase)
            || !host.Contains('.'))
        {
            return true;
        }

        IPAddress[] addresses;

        try
        {
            addresses = await Dns.GetHostAddressesAsync(host, cancellationToken);
        }
        catch (SocketException)
        {
            // Unresolvable now: the connection fails on its own and says nothing about the network.
            return false;
        }

        return addresses.Any(Is);
    }

    /// <summary>True when the address is loopback, private (10/8, 172.16/12, 192.168/16,
    /// fc00::/7), link-local, CGNAT (100.64/10), unspecified or multicast, IPv4-mapped included.</summary>
    public static bool Is(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any))
        {
            return true;
        }

        var bytes = address.GetAddressBytes();

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast
                || (bytes[0] & 0xFE) == 0xFC;
        }

        return bytes[0] == 10
            || bytes[0] == 127
            || bytes[0] == 0
            || bytes[0] >= 224
            || bytes[0] == 169 && bytes[1] == 254
            || bytes[0] == 172 && bytes[1] is >= 16 and <= 31
            || bytes[0] == 192 && bytes[1] == 168
            || bytes[0] == 100 && bytes[1] is >= 64 and <= 127;
    }
}
