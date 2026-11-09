// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSail.Data;

namespace NSail.Backup.Tests;

public sealed class BackupCredentialTests
{
    [Fact]
    public async Task UnderMultiDbTheVerbRefusesInsteadOfResolvingAContext()
    {
        var builder = Host.CreateApplicationBuilder();

        builder.Services.AddSingleton(new TenancyOptions { Mode = TenancyMode.MultiDb });

        using var host = builder.Build();

        var error = new StringWriter();
        var previous = Console.Error;

        Console.SetError(error);

        try
        {
            var code = await InstallConsole.Run<ProbeDbContext>(host, ["backup"]);

            Assert.Equal(4, code);
            Assert.Contains("not covered", error.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Console.SetError(previous);
        }
    }

    [Fact]
    public void TheDumpCredentialComesFromTheBackupSection()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [InstallBackup.ConnectionStringKey] = "Host=localhost;Username=postgres;Database=x" })
            .Build();

        Assert.Contains("Username=postgres", InstallBackup.DumpConnection(configuration), StringComparison.Ordinal);
        Assert.Null(InstallBackup.DumpConnection(new ConfigurationBuilder().Build()));
    }

    sealed class ProbeDbContext : DbContext
    {
    }

    [Fact]
    public async Task ACancelKillsTheChildAndSaysCancelled()
    {
        using var cancel = new CancellationTokenSource();
        var running = Postgres.Run("sleep", ["30"], null, cancel.Token);

        while (!Postgres.ChildRunning)
        {
            await Task.Delay(20);
        }

        await cancel.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running);
        Assert.False(Postgres.ChildRunning);
    }
}
