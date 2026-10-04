// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Microsoft.EntityFrameworkCore;

namespace NSail.Backup.Tests;

public sealed class InstallBackupTests
{
    [Fact]
    public void TheProductIsNamedByItsOwnContext()
    {
        Assert.Equal("Optical", InstallBackup.ProductOf<OpticalDbContext>());
    }

    [Fact]
    public void AContextThatDoesNotFollowTheConventionKeepsItsOwnName()
    {
        Assert.Equal("Ledger", InstallBackup.ProductOf<Ledger>());
    }

    sealed class OpticalDbContext : DbContext
    {
    }

    sealed class Ledger : DbContext
    {
    }
}
