// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Net;
using System.Net.Http.Json;
using Npgsql;
using NSail.Data;

namespace NSail.BaseServices.WebApi.Tests;

/// <summary>A column-wall cell that declares its roster serves only who it declares: a slug the
/// proxy still sends here and the roster does not name is a 404 with nothing planted for it, and
/// a roster that cannot be read fails the request rather than serving a slug nobody confirmed.
/// Through the real pipeline, with a seed registered, so "nothing planted" is a row count.</summary>
public sealed class SingleDbRosterTests : IAsyncLifetime
{
    readonly string _directory = Path.Combine(Path.GetTempPath(), "nsail-roster-" + Guid.NewGuid().ToString("N"));

    TenancyHost? _host;

    public Task InitializeAsync()
    {
        Directory.CreateDirectory(_directory);

        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        if (_host is not null)
        {
            await _host.DisposeAsync();
        }

        Directory.Delete(_directory, recursive: true);
    }

    [Fact]
    public async Task ASlugOffTheDeclaredRosterIsNotFoundAndNothingIsPlanted()
    {
        _host = await TenancyHost.Start(TenancyMode.SingleDb, bootstraps: true, tenants: "lumina");

        using var response = await _host.Client.SendAsync(_host.Get("/notes", "juli"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(0, await RowsOf("juli"));
    }

    [Fact]
    public async Task ASlugOnTheDeclaredRosterIsPlantedOnItsFirstTouch()
    {
        _host = await TenancyHost.Start(TenancyMode.SingleDb, bootstraps: true, tenants: "lumina");

        Assert.Equal(["welcome lumina"], await Read("lumina"));
    }

    // The sale's half: whoever provisions a tenant files it before its first visit, and the
    // next request reads the file again — no restart between the two.
    [Fact]
    public async Task ASlugFiledAfterStartupIsServedOnTheNextRequest()
    {
        var file = Path.Combine(_directory, "tenants");

        await File.WriteAllTextAsync(file, "lumina\n");

        _host = await TenancyHost.Start(TenancyMode.SingleDb, bootstraps: true, tenantsFile: file);

        using (var refused = await _host.Client.SendAsync(_host.Get("/notes", "vision")))
        {
            Assert.Equal(HttpStatusCode.NotFound, refused.StatusCode);
        }

        await File.WriteAllTextAsync(file, "lumina\nvision\n");

        Assert.Equal(["welcome vision"], await Read("vision"));
    }

    // A file that is named and empty declares nobody, which is not the same as declaring no
    // roster at all.
    [Fact]
    public async Task AnEmptyRosterFileServesNobody()
    {
        var file = Path.Combine(_directory, "tenants");

        await File.WriteAllTextAsync(file, string.Empty);

        _host = await TenancyHost.Start(TenancyMode.SingleDb, bootstraps: true, tenantsFile: file);

        using var response = await _host.Client.SendAsync(_host.Get("/notes", "lumina"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(0, await RowsOf("lumina"));
    }

    [Fact]
    public async Task ARosterFileThatCannotBeReadFailsTheRequestAndPlantsNothing()
    {
        _host = await TenancyHost.Start(
            TenancyMode.SingleDb,
            bootstraps: true,
            tenantsFile: Path.Combine(_directory, "missing"));

        // The test server rethrows what the pipeline did not handle, and a host behind Kestrel
        // turns the same throw into a 500: either way, no answer the slug was known by.
        try
        {
            using var response = await _host.Client.SendAsync(_host.Get("/notes", "lumina"));

            Assert.False(response.IsSuccessStatusCode);
        }
        catch (InvalidOperationException refusal)
        {
            Assert.Contains("Tenancy:TenantsFile", refusal.Message, StringComparison.Ordinal);
        }

        Assert.Equal(0, await RowsOf("lumina"));
    }

    async Task<IReadOnlyList<string>> Read(string tenant)
    {
        using var response = await _host!.Client.SendAsync(_host.Get("/notes", tenant));

        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<List<string>>())!;
    }

    // Past EF, because the filter would hide another tenant's rows from any scope that could ask.
    async Task<int> RowsOf(string tenant)
    {
        await using var connection = new NpgsqlConnection(
            ConnectionStrings.Complete($"Host=localhost;Username=postgres;Database={_host!.DatabaseOf(null)}"));

        await connection.OpenAsync();

        await using var query = new NpgsqlCommand("SELECT count(*) FROM \"Notes\" WHERE \"TenantId\" = @tenant", connection);

        query.Parameters.AddWithValue("tenant", Tenant.For(tenant).RowKey);

        return (int)(long)(await query.ExecuteScalarAsync())!;
    }
}
