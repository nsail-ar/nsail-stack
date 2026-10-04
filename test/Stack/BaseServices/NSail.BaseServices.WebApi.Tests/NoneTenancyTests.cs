// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using System.Net;
using System.Net.Http.Json;
using NSail.Data;

namespace NSail.BaseServices.WebApi.Tests;

/// <summary>The other half of nsail#357: under <c>None</c> nothing reads the header, and an
/// install behaves exactly as it did before the seam resolved anything.</summary>
public sealed class NoneTenancyTests : IAsyncLifetime
{
    TenancyHost _host = null!;

    public async Task InitializeAsync()
    {
        _host = await TenancyHost.Start(TenancyMode.None);
    }

    public async Task DisposeAsync()
    {
        await _host.DisposeAsync();
    }

    [Fact]
    public async Task TheInstallsOwnDatabaseIsMigratedAtStartupAsBefore()
    {
        Assert.Equal(["20260824000000_Notes"], await _host.AppliedMigrations(null));
    }

    // A header nobody reads is a header that changes nothing: same database, same answer, and
    // a slug that names no tenant anywhere is not a 404 here because nothing looked.
    [Theory]
    [InlineData(null)]
    [InlineData("lumina")]
    [InlineData("ghost")]
    public async Task TheHeaderIsNeverRead(string? tenant)
    {
        using var response = await _host.Client.SendAsync(_host.Get("/where", tenant));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var where = (await response.Content.ReadFromJsonAsync<Where>())!;

        Assert.Null(where.Slug);
        Assert.Null(where.Database);
        Assert.Equal(_host.DatabaseOf(null), where.Connection);
    }

    // The other half of the wall's no-op: with no tenant to stamp, a ticket names none, and
    // nothing is installed that would check one — so a header nobody reads cannot refuse it.
    [Theory]
    [InlineData(null)]
    [InlineData("lumina")]
    public async Task ATicketNamesNoTenantAndNothingChecksOne(string? tenant)
    {
        var ticket = await _host.SignIn(tenant: null);

        using var response = await _host.Client.SendAsync(_host.Get("/guarded", tenant, ticket: ticket));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
