// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Net;
using System.Net.Sockets;

namespace NSail.Security.Tests;

public class PrivateHostTests
{
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("127.8.9.10")]
    [InlineData("10.0.0.5")]
    [InlineData("172.16.0.1")]
    [InlineData("172.31.255.255")]
    [InlineData("192.168.1.1")]
    [InlineData("169.254.169.254")]
    [InlineData("100.64.0.1")]
    [InlineData("100.127.255.255")]
    [InlineData("0.0.0.0")]
    [InlineData("224.0.0.1")]
    [InlineData("::1")]
    [InlineData("[::1]")]
    [InlineData("::")]
    [InlineData("fe80::1")]
    [InlineData("fc00::1")]
    [InlineData("fd12:3456::1")]
    [InlineData("ff02::1")]
    [InlineData("::ffff:127.0.0.1")]
    [InlineData("[::ffff:10.0.0.5]")]
    [InlineData("::ffff:169.254.169.254")]
    public async Task RefusesAPrivateLiteral(string host)
    {
        Assert.True(await PrivateHost.Is(host));
    }

    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("172.32.0.1")]
    [InlineData("100.128.0.1")]
    [InlineData("192.169.0.1")]
    [InlineData("2001:4860:4860::8888")]
    [InlineData("::ffff:8.8.8.8")]
    public async Task AcceptsAPublicLiteral(string host)
    {
        Assert.False(await PrivateHost.Is(host));
    }

    [Theory]
    [InlineData("localhost")]
    [InlineData("LOCALHOST.")]
    [InlineData("minio.localhost")]
    [InlineData("printer.local")]
    [InlineData("metadata.google.internal")]
    [InlineData("smtp")]
    public async Task RefusesALocalName(string host)
    {
        Assert.True(await PrivateHost.Is(host));
    }

    [Theory]
    [InlineData("")]
    [InlineData("[]")]
    [InlineData(".")]
    public async Task RefusesAnEmptyHost(string host)
    {
        Assert.True(await PrivateHost.Is(host));
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("10.0.0.5")]
    [InlineData("169.254.169.254")]
    [InlineData("::1")]
    [InlineData("::ffff:192.168.1.1")]
    public async Task RefusesANameThatResolvesToAPrivateAddress(string address)
    {
        Assert.True(await PrivateHost.Is("smtp.example.com", Resolving(address), CancellationToken.None));
    }

    [Fact]
    public async Task RefusesANameWhenAnyOfItsAddressesIsPrivate()
    {
        Assert.True(await PrivateHost.Is("smtp.example.com", Resolving("8.8.8.8", "10.0.0.5"), CancellationToken.None));
    }

    [Fact]
    public async Task AcceptsANameThatResolvesOutside()
    {
        Assert.False(await PrivateHost.Is("smtp.gmail.com", Resolving("142.250.0.108", "2a00:1450:400c:c0b::6c"),
            CancellationToken.None));
    }

    [Fact]
    public async Task ResolvesTheHostWithoutItsTrailingDot()
    {
        string? asked = null;

        await PrivateHost.Is("smtp.gmail.com.", (host, _) =>
        {
            asked = host;
            return Task.FromResult(new[] { IPAddress.Parse("142.250.0.108") });
        }, CancellationToken.None);

        Assert.Equal("smtp.gmail.com", asked);
    }

    [Fact]
    public async Task DoesNotRefuseANameThatDoesNotResolve()
    {
        Assert.False(await PrivateHost.Is("nowhere.invalid",
            (_, _) => throw new SocketException((int)SocketError.HostNotFound), CancellationToken.None));
    }

    private static Func<string, CancellationToken, Task<IPAddress[]>> Resolving(params string[] addresses)
    {
        return (_, _) => Task.FromResult(addresses.Select(IPAddress.Parse).ToArray());
    }
}
