// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Security.Cryptography;
using NSail.Data;
using NSail.Security;

namespace NSail.BaseServices.WebApi.Tests;

/// <summary>What <c>AddTenantKeys</c> composes: no shared key outside Development, a fixed one
/// inside it, and each tenant sealing with its own file.</summary>
public sealed class TenantKeysTests : IDisposable
{
    readonly string _directory = Path.Combine(Path.GetTempPath(), "nsail-keys-" + Guid.NewGuid().ToString("N"));

    public TenantKeysTests()
    {
        Directory.CreateDirectory(_directory);
    }

    public void Dispose()
    {
        Directory.Delete(_directory, recursive: true);
    }

    [Fact]
    public void OutsideDevelopmentAHostWithoutADirectoryRefusesToCompose()
    {
        var builder = Builder(Environments.Production, directory: null);

        Assert.Throws<InvalidOperationException>(builder.AddTenantKeys);
    }

    [Fact]
    public void DevelopmentWithoutADirectorySealsUnderAFixedKey()
    {
        var builder = Builder(Environments.Development, directory: null);
        builder.AddTenantKeys();
        using var provider = builder.Services.BuildServiceProvider();
        var keys = provider.GetRequiredService<KeyProtector>();

        Assert.Equal("secret", keys.Unprotect(keys.Protect("secret")));
    }

    [Fact]
    public void EachTenantSealsWithItsOwnFile()
    {
        File.WriteAllText(Path.Combine(_directory, "lumina.key"), "lumina-key");
        File.WriteAllText(Path.Combine(_directory, "juli.key"), "juli-key");
        var builder = Builder(Environments.Production, _directory);
        builder.AddTenantKeys();
        using var provider = builder.Services.BuildServiceProvider();
        var keys = provider.GetRequiredService<KeyProtector>();

        AmbientTenant.Enter(Tenant.For("lumina", "lumina-db"));
        var sealedByLumina = keys.Protect("secret");

        Assert.Equal("secret", keys.Unprotect(sealedByLumina));

        AmbientTenant.Enter(Tenant.For("juli", "juli-db"));

        Assert.ThrowsAny<CryptographicException>(() => keys.Unprotect(sealedByLumina));
    }

    [Fact]
    public void ATenantWithoutAFileMakesTheSealThrow()
    {
        var builder = Builder(Environments.Production, _directory);
        builder.AddTenantKeys();
        using var provider = builder.Services.BuildServiceProvider();
        var keys = provider.GetRequiredService<KeyProtector>();

        AmbientTenant.Enter(Tenant.For("nobody", "nobody-db"));

        Assert.Throws<MissingEncryptionKeyException>(() => keys.Protect("secret"));
    }

    static WebApplicationBuilder Builder(string environment, string? directory)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environment });

        if (directory is not null)
        {
            builder.Configuration["Security:KeysDirectory"] = directory;
        }

        return builder;
    }
}
