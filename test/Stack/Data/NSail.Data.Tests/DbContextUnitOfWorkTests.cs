// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSail.Messaging;
using NSail.Messaging.Runtime.Sending;
using NSail.Messaging.Runtime.UnitOfWork;

namespace NSail.Data.Tests;

public sealed class DbContextUnitOfWorkTests
{
    // The caller hangs up between the handler's return and the commit: the token the send was
    // given is already cancelled when the unit commits, and the work it did has to stand.
    [Fact]
    public async Task a_send_whose_caller_left_after_its_handler_returned_commits_what_it_wrote()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var logs = new ErrorLog();
        await using var provider = Host(connection, logs);

        await using (var scope = provider.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<NoteDbContext>().Database.EnsureCreatedAsync();
        }

        var sender = new InProcessSender<Noted>(provider, provider.GetRequiredService<IServiceScopeFactory>());
        using var caller = new CancellationTokenSource();

        await sender.Run(
            async (services, cancellationToken) =>
            {
                var db = services.GetRequiredService<NoteDbContext>();

                db.Notes.Add(new Note { Id = Guid.NewGuid(), Text = "written" });

                await db.SaveChangesAsync(cancellationToken);

                await caller.CancelAsync();
            },
            caller.Token);

        await using (var check = provider.CreateAsyncScope())
        {
            Assert.Equal(["written"], await check.ServiceProvider.GetRequiredService<NoteDbContext>().Notes.Select(note => note.Text).ToListAsync());
        }

        Assert.Empty(logs.Errors);
    }

    // The cancellation is observed while the handler still runs, so its work is not done and
    // nothing it wrote stands.
    [Fact]
    public async Task a_send_whose_handler_stops_on_the_cancellation_leaves_nothing_written()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        await using var provider = Host(connection, new ErrorLog());

        await using (var scope = provider.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<NoteDbContext>().Database.EnsureCreatedAsync();
        }

        var sender = new InProcessSender<Noted>(provider, provider.GetRequiredService<IServiceScopeFactory>());
        using var caller = new CancellationTokenSource();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sender.Run(
            async (services, cancellationToken) =>
            {
                var db = services.GetRequiredService<NoteDbContext>();

                db.Notes.Add(new Note { Id = Guid.NewGuid(), Text = "abandoned" });

                await db.SaveChangesAsync(cancellationToken);

                await caller.CancelAsync();

                cancellationToken.ThrowIfCancellationRequested();
            },
            caller.Token));

        await using (var check = provider.CreateAsyncScope())
        {
            Assert.Empty(await check.ServiceProvider.GetRequiredService<NoteDbContext>().Notes.ToListAsync());
        }
    }

    [Fact]
    public async Task a_failing_commit_disposes_the_transaction_and_rollback_is_safe()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<DbContext>().UseSqlite(connection).Options;
        await using var db = new DbContext(options);
        var work = (IUnitOfWork)Activator.CreateInstance(typeof(OrgScopeProvider).Assembly.GetType("NSail.Data.DbContextUnitOfWork")!, db)!;

        await work.Begin(CancellationToken.None);

        var transaction = db.Database.CurrentTransaction!;
        var disposed = false;
        var throwing = new ThrowingTransaction(transaction, () => disposed = true);

        work.GetType().GetField("_transaction", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(work, throwing);

        await Assert.ThrowsAsync<InvalidOperationException>(() => work.Commit(CancellationToken.None));

        Assert.True(disposed);
        await work.Rollback();
    }

    sealed class ThrowingTransaction(IDbContextTransaction inner, Action onDispose) : IDbContextTransaction
    {
        public Guid TransactionId
        {
            get { return inner.TransactionId; }
        }

        public void Commit()
        {
            throw new InvalidOperationException();
        }

        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException();
        }

        public void Rollback()
        {
            inner.Rollback();
        }

        public Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            return inner.RollbackAsync(cancellationToken);
        }

        public void Dispose()
        {
            onDispose();
            inner.Dispose();
        }

        public ValueTask DisposeAsync()
        {
            onDispose();

            return inner.DisposeAsync();
        }
    }

    static ServiceProvider Host(SqliteConnection connection, ErrorLog logs)
    {
        var services = new ServiceCollection();

        services.AddDbContext<NoteDbContext>(options => options
            .UseSqlite(connection)
            .UseLoggerFactory(LoggerFactory.Create(logging => logging.AddProvider(logs))));
        services.AddScoped<DbContext>(provider => provider.GetRequiredService<NoteDbContext>());
        services.AddUnitOfWork();

        return services.BuildServiceProvider();
    }

    sealed record Noted : IMessage;

    sealed class Note
    {
        public Guid Id { get; set; }

        public string Text { get; set; } = "";
    }

    sealed class NoteDbContext(DbContextOptions<NoteDbContext> options) : DbContext(options)
    {
        public DbSet<Note> Notes
        {
            get { return Set<Note>(); }
        }
    }

    sealed class ErrorLog : ILoggerProvider, ILogger
    {
        public List<string> Errors { get; } = [];

        public ILogger CreateLogger(string categoryName)
        {
            return this;
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel >= LogLevel.Error;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(logLevel))
            {
                lock (Errors)
                {
                    Errors.Add(formatter(state, exception));
                }
            }
        }

        public void Dispose()
        {
        }
    }
}
