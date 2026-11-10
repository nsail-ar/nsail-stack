// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Security.Cryptography;

namespace NSail.Security.Tests;

public sealed class TenantKeyTests : IDisposable
{
    readonly string _directory = Directory.CreateTempSubdirectory("nsail-keys").FullName;
    string? _tenant;

    KeyProtector Protector()
    {
        return new KeyProtector(new TenantKeyFiles(_directory, () => _tenant));
    }

    public void Dispose()
    {
        Directory.Delete(_directory, true);
    }

    [Fact]
    public void Each_tenant_opens_only_its_own()
    {
        File.WriteAllText(Path.Combine(_directory, "a.key"), "key-a\n");
        File.WriteAllText(Path.Combine(_directory, "b.key"), "key-b");
        var protector = Protector();

        _tenant = "a";
        var sealedValue = protector.Protect("secret");
        Assert.Equal("secret", protector.Unprotect(sealedValue));

        _tenant = "b";
        Assert.ThrowsAny<CryptographicException>(() => protector.Unprotect(sealedValue));
    }

    [Fact]
    public void A_tenant_without_a_key_refuses_to_seal_and_to_open()
    {
        var protector = Protector();
        _tenant = "nokey";

        Assert.Throws<MissingEncryptionKeyException>(() => protector.Protect("x"));
        Assert.Throws<MissingEncryptionKeyException>(() => protector.Unprotect("v2.AAAA"));

        _tenant = null;
        Assert.Throws<MissingEncryptionKeyException>(() => protector.Protect("x"));
    }

    [Fact]
    public void A_key_written_later_is_honoured()
    {
        var protector = Protector();
        _tenant = "late";
        Assert.Throws<MissingEncryptionKeyException>(() => protector.Protect("x"));

        File.WriteAllText(Path.Combine(_directory, "late.key"), "k");

        Assert.Equal("x", protector.Unprotect(protector.Protect("x")));
    }

    [Fact]
    public void A_slug_cannot_leave_the_directory()
    {
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(_directory)!, "outside.key"), "k");
        var protector = Protector();
        _tenant = "../outside";

        Assert.Throws<MissingEncryptionKeyException>(() => protector.Protect("x"));
    }
}
