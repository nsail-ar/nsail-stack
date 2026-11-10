// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace NSail.Data.Tests;

/// <summary>Under MultiDb every tenant's <c>RowKey</c> is the install's own, so the memo has to
/// be told them apart by something else — one process memo serves every tenant's scope.</summary>
public sealed class TenantStatesMultiDbTests
{
    [Fact]
    public async Task Two_tenants_of_one_memo_each_see_their_own_state_under_MultiDb()
    {
        var memo = new TenantStateMemo(new TenancyOptions { StateCacheSeconds = 120 }, TimeProvider.System);
        var options = new TenancyOptions { Mode = TenancyMode.MultiDb, Tenants = "alfa,beta" };

        await using var alfa = await Database(TenantState.Suspended);
        await using var beta = await Database(TenantState.Active);

        var alfaStates = States(options, "alfa", alfa.Context, memo);
        var betaStates = States(options, "beta", beta.Context, memo);

        Assert.True(await alfaStates.IsSuspended());
        Assert.False(await betaStates.IsSuspended());
        Assert.True(await alfaStates.IsSuspended());
    }

    [Fact]
    public async Task Under_SingleDb_a_tenant_with_no_row_is_read_once_per_window()
    {
        var memo = new TenantStateMemo(new TenancyOptions { StateCacheSeconds = 120 }, TimeProvider.System);
        var reads = 0;
        var key = "x";

        for (var i = 0; i < 3; i++)
        {
            await memo.Of(key, _ =>
            {
                reads++;

                return Task.FromResult<TenantState?>(null);
            });
        }

        Assert.Equal(1, reads);
    }

    [Fact]
    public void The_state_key_is_the_slug_under_MultiDb_and_the_row_key_under_SingleDb()
    {
        var multi = Provider(new TenancyOptions { Mode = TenancyMode.MultiDb, Tenants = "alfa" }, "alfa");
        var single = Provider(new TenancyOptions { Mode = TenancyMode.SingleDb, Tenants = "alfa" }, "alfa");

        Assert.Equal("alfa", multi.StateKey);
        Assert.Equal(TenantKey.For("alfa").ToString(), single.StateKey);
    }

    static ResolvedTenancyProvider Provider(TenancyOptions options, string slug)
    {
        var provider = new ResolvedTenancyProvider(options);

        provider.Enter(options.Mode == TenancyMode.MultiDb ? Tenant.For(slug, slug) : Tenant.For(slug));

        return provider;
    }

    static TenantStates States(TenancyOptions options, string slug, DbContext db, TenantStateMemo memo)
    {
        return new TenantStates(db, Provider(options, slug), memo, TimeProvider.System);
    }

    static async Task<Held> Database(string status)
    {
        var connection = new SqliteConnection("DataSource=:memory:");

        await connection.OpenAsync();

        var context = new StateContext(new DbContextOptionsBuilder<StateContext>().UseSqlite(connection).Options);

        await context.Database.EnsureCreatedAsync();

        context.Add(new TenantState { TenantId = Guid.Empty, Status = status });
        await context.SaveChangesAsync();

        return new Held(connection, context);
    }

    sealed class Held : IAsyncDisposable
    {
        readonly SqliteConnection _connection;

        public Held(SqliteConnection connection, StateContext context)
        {
            _connection = connection;
            Context = context;
        }

        public StateContext Context { get; }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    sealed class StateContext : DbContext
    {
        public StateContext(DbContextOptions<StateContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TenantState>().HasKey(row => row.TenantId);
            modelBuilder.Entity<TenantState>().Property(row => row.TenantId).ValueGeneratedNever();
        }
    }
}
