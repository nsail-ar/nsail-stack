// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace NSail.Data.Tests;

/// <summary>A pooled connection whose Postgres session ended while it sat in the pool, on a real
/// Postgres under SingleDb: the tenant write is the first statement it runs, so that is where the
/// dead session is found.</summary>
// Alone: these tests follow one pooled connection from scope to scope, and the pools are the
// process's, emptied by any class that clears them or creates a database (Npgsql's creator calls
// ClearAllPools), which would hand the next scope a session the test never saw.
[Collection(nameof(TenantSessionEndedTests))]
public sealed class TenantSessionEndedTests : IAsyncLifetime
{
    static readonly Tenant Lumina = Tenant.For("lumina");
    static readonly Tenant Vision = Tenant.For("vision");

    readonly string _database = "nsail4430_" + Guid.NewGuid().ToString("N")[..12];

    string _connectionString = null!;

    public async Task InitializeAsync()
    {
        await Admin($"CREATE DATABASE \"{_database}\"");

        _connectionString = $"Host=localhost;Username=postgres;Database={_database}";

        await using var provider = Host(_connectionString);

        await using (var scope = provider.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<SessionDbContext>().Database.EnsureCreatedAsync();
        }

        await Add(provider, Lumina, "lumina's");
        await Add(provider, Vision, "vision's");
    }

    public async Task DisposeAsync()
    {
        NpgsqlConnection.ClearAllPools();

        await Admin($"DROP DATABASE IF EXISTS \"{_database}\" WITH (FORCE)");
    }

    [Fact]
    public async Task A_scope_whose_pooled_session_ended_is_served_on_a_fresh_one_under_its_tenant()
    {
        await using var provider = Host(_connectionString);

        var ended = (await Session(provider, Lumina)).Pid;

        await Terminate(ended);

        var session = await Session(provider, Lumina);

        Assert.NotEqual(ended, session.Pid);
        Assert.Equal(Lumina.RowKey.ToString("D"), session.Tenant);

        await using var scope = provider.CreateAsyncScope();

        Enter(scope, Lumina);

        Assert.Equal(["lumina's"], await scope.ServiceProvider.GetRequiredService<SessionDbContext>().Notes.Select(note => note.Text).ToListAsync());
    }

    // The replacement session goes back to the pool like any other, so the next scope to take
    // it carries no tenant of the one that opened it.
    [Fact]
    public async Task A_scope_that_resolved_no_tenant_reads_nothing_on_the_replacement_session()
    {
        await using var provider = Host(_connectionString);

        await Terminate((await Session(provider, Lumina)).Pid);

        var replacement = (await Session(provider, Lumina)).Pid;

        await using var scope = provider.CreateAsyncScope();

        var db = scope.ServiceProvider.GetRequiredService<SessionDbContext>();

        await db.Database.OpenConnectionAsync();

        Assert.Equal(replacement, await db.Database.SqlQueryRaw<int>("SELECT pg_backend_pid() AS \"Value\"").SingleAsync());
        Assert.Null(await db.Database.SqlQueryRaw<string?>($"SELECT nullif(current_setting('{TenantPolicies.Setting}', true), '') AS \"Value\"").SingleAsync());
        Assert.Empty(await db.Notes.ToListAsync());
    }

    // The fresh session is not opened past the first: an open that cannot be made fails before
    // TenantSession runs, so it fails once with the server's own answer. A missing role is refused
    // as 28000 by a server that trusts the client and as 28P01 by one that asks for a password,
    // so the login case asserts the SqlState's class only.
    [Theory]
    [InlineData("Database=nsail4430_missing", PostgresErrorCodes.InvalidCatalogName)]
    [InlineData("Username=nsail4430_missing", "28")]
    public async Task An_open_no_second_session_changes_fails_once(string wrong, string sqlState)
    {
        var builder = new NpgsqlConnectionStringBuilder(_connectionString);
        var refused = new NpgsqlConnectionStringBuilder(wrong);

        builder.Database = refused.Database ?? builder.Database;
        builder.Username = refused.Username ?? builder.Username;

        var opens = new OpenCounter();

        await using var provider = Host(builder.ConnectionString, opens);
        await using var scope = provider.CreateAsyncScope();

        Enter(scope, Lumina);

        var failure = await Assert.ThrowsAsync<PostgresException>(
            () => scope.ServiceProvider.GetRequiredService<SessionDbContext>().Notes.ToListAsync());

        Assert.StartsWith(sqlState, failure.SqlState);
        Assert.Equal(1, opens.Opening);
        Assert.Equal(1, opens.Failed);
    }

    // One more attempt and no loop: a session that is dead on the second open too is the caller's.
    [Fact]
    public async Task A_session_that_ends_on_both_attempts_reaches_the_caller()
    {
        var connection = new EndingConnection();

        await Assert.ThrowsAnyAsync<DbException>(() => EnterSession(connection));

        Assert.Equal(2, connection.Opens);
        Assert.Equal(2, connection.Writes);
    }

    // And a failure that leaves the session standing is not a dead session: no second open.
    [Fact]
    public async Task A_write_that_fails_on_a_live_session_is_not_retried()
    {
        var connection = new EndingConnection { Ends = false };

        await Assert.ThrowsAnyAsync<DbException>(() => EnterSession(connection));

        Assert.Equal(1, connection.Opens);
        Assert.Equal(1, connection.Writes);
    }

    static Task EnterSession(EndingConnection connection)
    {
        connection.Open();

        var enter = typeof(TenancyProvider).Assembly.GetType("NSail.Data.TenantSession")!.GetMethod("Enter")!;

        return (Task)enter.Invoke(null, [connection, Lumina.RowKey, CancellationToken.None])!;
    }

    static ServiceProvider Host(string connectionString, IInterceptor? interceptor = null)
    {
        var services = new ServiceCollection();

        services.AddDataAccess<SessionDbContext>(connectionString, new TenancyOptions { Mode = TenancyMode.SingleDb });

        if (interceptor is not null)
        {
            services.ConfigureDbContext<SessionDbContext>(options => options.AddInterceptors(interceptor));
        }

        return services.BuildServiceProvider();
    }

    static void Enter(AsyncServiceScope scope, Tenant tenant)
    {
        scope.ServiceProvider.GetRequiredService<ResolvedTenancyProvider>().Enter(tenant);
    }

    static async Task Add(ServiceProvider provider, Tenant tenant, string text)
    {
        await using var scope = provider.CreateAsyncScope();

        Enter(scope, tenant);

        var db = scope.ServiceProvider.GetRequiredService<SessionDbContext>();

        db.Notes.Add(new SessionNote { Id = Guid.NewGuid(), Text = text });

        await db.SaveChangesAsync();
    }

    static async Task<SessionRow> Session(ServiceProvider provider, Tenant tenant)
    {
        await using var scope = provider.CreateAsyncScope();

        Enter(scope, tenant);

        return await scope.ServiceProvider.GetRequiredService<SessionDbContext>().Database
            .SqlQueryRaw<SessionRow>($"SELECT pg_backend_pid() AS \"Pid\", current_setting('{TenantPolicies.Setting}', true) AS \"Tenant\"")
            .SingleAsync();
    }

    // From a connection of its own, and only back once the backend is gone, so the pooled
    // connection is certain to be holding a dead session when the next scope takes it.
    static async Task Terminate(int pid)
    {
        await using var connection = new NpgsqlConnection(ConnectionStrings.Complete("Host=localhost;Username=postgres;Database=postgres;Pooling=false"));

        await connection.OpenAsync();

        await using var terminate = new NpgsqlCommand("SELECT pg_terminate_backend(@pid, 5000)", connection);

        terminate.Parameters.AddWithValue("pid", pid);

        Assert.Equal(true, await terminate.ExecuteScalarAsync());
    }

    static async Task Admin(string sql)
    {
        await using var connection = new NpgsqlConnection(ConnectionStrings.Complete("Host=localhost;Username=postgres;Database=postgres;Pooling=false"));

        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(sql, connection);

        await command.ExecuteNonQueryAsync();
    }

    [SuppressMessage("Performance", "CA1812", Justification = "Materialized by SqlQueryRaw.")]
    sealed class SessionRow
    {
        public int Pid { get; set; }

        public string? Tenant { get; set; }
    }

    sealed class OpenCounter : DbConnectionInterceptor
    {
        public int Opening { get; private set; }

        public int Failed { get; private set; }

        public override ValueTask<InterceptionResult> ConnectionOpeningAsync(
            DbConnection connection,
            ConnectionEventData eventData,
            InterceptionResult result,
            CancellationToken cancellationToken = default)
        {
            Opening++;

            return ValueTask.FromResult(result);
        }

        public override Task ConnectionFailedAsync(
            DbConnection connection,
            ConnectionErrorEventData eventData,
            CancellationToken cancellationToken = default)
        {
            Failed++;

            return Task.CompletedTask;
        }
    }

    // A connection whose every write finds its session ended (or, with Ends off, refused on a
    // session that stays open), counting the opens and writes TenantSession makes.
    sealed class EndingConnection : DbConnection
    {
        ConnectionState _state = ConnectionState.Closed;

        public bool Ends { get; init; } = true;

        public int Opens { get; private set; }

        public int Writes { get; private set; }

        [AllowNull]
        public override string ConnectionString { get; set; } = "";

        public override string Database
        {
            get { return "ending"; }
        }

        public override string DataSource
        {
            get { return "ending"; }
        }

        public override string ServerVersion
        {
            get { return "0"; }
        }

        public override ConnectionState State
        {
            get { return _state; }
        }

        public override void Open()
        {
            Opens++;
            _state = ConnectionState.Open;
        }

        public override void Close()
        {
            _state = ConnectionState.Closed;
        }

        public override void ChangeDatabase(string databaseName)
        {
            throw new NotSupportedException();
        }

        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
        {
            throw new NotSupportedException();
        }

        protected override DbCommand CreateDbCommand()
        {
            return new EndingCommand(this);
        }

        internal void Write()
        {
            Writes++;

            if (Ends)
            {
                _state = ConnectionState.Closed;
            }

            throw new NpgsqlException("The session ended.");
        }
    }

    sealed class EndingCommand(EndingConnection connection) : DbCommand
    {
        [AllowNull]
        public override string CommandText { get; set; } = "";

        public override int CommandTimeout { get; set; }

        public override CommandType CommandType { get; set; }

        public override bool DesignTimeVisible { get; set; }

        public override UpdateRowSource UpdatedRowSource { get; set; }

        protected override DbConnection? DbConnection { get; set; } = connection;

        protected override DbParameterCollection DbParameterCollection { get; } = new NpgsqlCommand().Parameters;

        protected override DbTransaction? DbTransaction { get; set; }

        public override void Cancel()
        {
        }

        public override int ExecuteNonQuery()
        {
            connection.Write();

            return 0;
        }

        public override object? ExecuteScalar()
        {
            throw new NotSupportedException();
        }

        public override void Prepare()
        {
        }

        protected override DbParameter CreateDbParameter()
        {
            return new NpgsqlParameter();
        }

        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
        {
            throw new NotSupportedException();
        }
    }
}

[CollectionDefinition(nameof(TenantSessionEndedTests), DisableParallelization = true)]
public sealed class TenantSessionEndedCollection
{
}

public sealed class SessionDbContext : DbContext, ITenanted, IHasOrgScope
{
    readonly TenancyProvider _tenancy;

    public SessionDbContext(DbContextOptions<SessionDbContext> options, TenancyProvider tenancy)
        : base(options)
    {
        _tenancy = tenancy;
    }

    public DbSet<SessionNote> Notes
    {
        get { return Set<SessionNote>(); }
    }

    public Guid? TenantId
    {
        get { return _tenancy.RowKey; }
    }

    public OrgScope OrgScope
    {
        get { return OrgScope.Everywhere; }
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.ApplyTenancy();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplySetups(this, [new SessionSetup()]);
    }
}

sealed class SessionSetup : IDbContextSetup
{
    public void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SessionNote>(note =>
        {
            note.ToTable("Notes");
            note.HasKey(entity => entity.Id);
        });
    }
}

public sealed class SessionNote
{
    public Guid Id { get; set; }

    public string Text { get; set; } = "";
}
