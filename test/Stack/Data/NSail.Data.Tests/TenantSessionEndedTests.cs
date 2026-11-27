// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using NSail.Messaging;
using NSail.Messaging.Runtime.Sending;

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

    // A scope that resolved no tenant still writes its (empty) setting first, so a session that
    // ended in the pool is found by that write and not by the caller's query.
    [Fact]
    public async Task A_scope_that_resolved_no_tenant_reads_nothing_when_its_pooled_session_ended()
    {
        await using var provider = Host(_connectionString);

        await Session(provider, Lumina);
        await TerminateAll();

        await using var scope = provider.CreateAsyncScope();

        Assert.Empty(await scope.ServiceProvider.GetRequiredService<SessionDbContext>().Notes.ToListAsync());
    }

    // A session that ends inside an open unit of work is not the tenant write's to recover: the
    // handler has run, so the send rolls back and the caller is told, and the handler is not run
    // again behind it. Through either type a handler may inject, since both are the one context
    // the unit of work enlists.
    [Theory]
    [InlineData(typeof(DbContext))]
    [InlineData(typeof(SessionDbContext))]
    public async Task A_session_that_ends_inside_a_send_fails_it_once_and_leaves_nothing(Type injected)
    {
        await using var provider = Host(_connectionString);
        await using var caller = provider.CreateAsyncScope();

        Enter(caller, Lumina);

        var sender = new InProcessSender<Noted>(provider, provider.GetRequiredService<IServiceScopeFactory>());
        var runs = 0;
        var terminated = false;

        await Assert.ThrowsAnyAsync<DbException>(() => sender.Run(
            async (services, cancellationToken) =>
            {
                runs++;

                var db = (DbContext)services.GetRequiredService(injected);

                db.Add(new SessionNote { Id = Guid.NewGuid(), Text = "uncommitted" });

                await db.SaveChangesAsync(cancellationToken);

                terminated = await Terminated(await db.Database.SqlQueryRaw<int>("SELECT pg_backend_pid() AS \"Value\"").SingleAsync(cancellationToken));
            },
            CancellationToken.None));

        Assert.True(terminated);
        Assert.Equal(1, runs);

        await using var check = provider.CreateAsyncScope();

        Enter(check, Lumina);

        Assert.Equal(["lumina's"], await check.ServiceProvider.GetRequiredService<SessionDbContext>().Notes.Select(note => note.Text).ToListAsync());
    }

    // The injected context is the one the send's transaction is open on, so a handler that needs
    // a transaction of its own opens it on a context of its own, which commits where it is written
    // and stands when the send rolls back.
    [Fact]
    public async Task A_handler_opens_a_transaction_of_its_own_on_a_context_of_its_own()
    {
        await using var provider = Host(_connectionString);
        await using var caller = provider.CreateAsyncScope();

        Enter(caller, Lumina);

        var sender = new InProcessSender<Noted>(provider, provider.GetRequiredService<IServiceScopeFactory>());

        await Assert.ThrowsAsync<OperationCanceledException>(() => sender.Run(
            async (services, cancellationToken) =>
            {
                var injected = services.GetRequiredService<SessionDbContext>();

                Assert.Same(injected, services.GetRequiredService<DbContext>());
                await Assert.ThrowsAsync<InvalidOperationException>(() => injected.Database.BeginTransactionAsync(cancellationToken));

                injected.Add(new SessionNote { Id = Guid.NewGuid(), Text = "rolled back" });

                await injected.SaveChangesAsync(cancellationToken);

                await using var own = await services.GetRequiredService<IDbContextFactory<SessionDbContext>>().CreateDbContextAsync(cancellationToken);
                await using var transaction = await own.Database.BeginTransactionAsync(cancellationToken);

                own.Add(new SessionNote { Id = Guid.NewGuid(), Text = "its own" });

                await own.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                throw new OperationCanceledException();
            },
            CancellationToken.None));

        await using var check = provider.CreateAsyncScope();

        Enter(check, Lumina);

        Assert.Equal(["its own", "lumina's"], await check.ServiceProvider.GetRequiredService<SessionDbContext>().Notes.Select(note => note.Text).OrderBy(text => text).ToListAsync());
    }

    // A statement the server refuses on a live session is the caller's at once, as Postgres said
    // it: the insert is sent once and the failure is not wrapped as transient.
    [Fact]
    public async Task A_unique_violation_is_sent_once_and_surfaces_as_itself()
    {
        var inserts = new InsertCounter();

        await using var provider = Host(_connectionString, inserts);
        await using var scope = provider.CreateAsyncScope();

        Enter(scope, Lumina);

        var db = scope.ServiceProvider.GetRequiredService<SessionDbContext>();
        var taken = await db.Notes.AsNoTracking().Select(note => note.Id).FirstAsync();

        db.Notes.Add(new SessionNote { Id = taken, Text = "again" });

        var refusal = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());

        Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>(refusal.InnerException).SqlState);
        Assert.Equal(1, inserts.Sent);
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
        Assert.True(await Terminated(pid));
    }

    static async Task<bool> Terminated(int pid)
    {
        await using var connection = new NpgsqlConnection(ConnectionStrings.Complete("Host=localhost;Username=postgres;Database=postgres;Pooling=false"));

        await connection.OpenAsync();

        await using var terminate = new NpgsqlCommand("SELECT pg_terminate_backend(@pid, 5000)", connection);

        terminate.Parameters.AddWithValue("pid", pid);

        return await terminate.ExecuteScalarAsync() is true;
    }

    // Every session on the test's database, as a server restart would end them, so whichever one
    // the pool hands the next scope is dead.
    async Task TerminateAll()
    {
        await using var connection = new NpgsqlConnection(ConnectionStrings.Complete("Host=localhost;Username=postgres;Database=postgres;Pooling=false"));

        await connection.OpenAsync();

        await using var terminate = new NpgsqlCommand(
            "SELECT count(*) FILTER (WHERE pg_terminate_backend(pid, 5000)) FROM pg_stat_activity WHERE datname = @database AND pid <> pg_backend_pid()",
            connection);

        terminate.Parameters.AddWithValue("database", _database);

        Assert.True((long)(await terminate.ExecuteScalarAsync())! > 0);
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

    sealed class InsertCounter : DbCommandInterceptor
    {
        public int Sent { get; private set; }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("INSERT", StringComparison.Ordinal))
            {
                Sent++;
            }

            return ValueTask.FromResult(result);
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("INSERT", StringComparison.Ordinal))
            {
                Sent++;
            }

            return ValueTask.FromResult(result);
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

public sealed class Noted : IMessage
{
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
