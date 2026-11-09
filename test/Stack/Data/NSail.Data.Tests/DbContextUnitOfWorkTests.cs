// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using NSail.Messaging.Runtime.UnitOfWork;

namespace NSail.Data.Tests;

public sealed class DbContextUnitOfWorkTests
{
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
}
