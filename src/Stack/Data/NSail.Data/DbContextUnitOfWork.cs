// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using NSail.Messaging.Runtime.UnitOfWork;

namespace NSail.Data;

// The scope's DbContext, enlisted in the send's own transaction. A handler keeps calling
// SaveChanges wherever it always did: with a transaction already open, EF stops wrapping each
// call in one of its own, so every save in the operation — the composing handler's and every
// nested handler's — becomes visible to the rest of the flow immediately and durable only when
// the outermost send commits.
sealed class DbContextUnitOfWork : IUnitOfWork
{
    readonly DbContext _db;

    IDbContextTransaction? _transaction;

    public DbContextUnitOfWork(DbContext db)
    {
        _db = db;
    }

    public async Task Begin(CancellationToken cancellationToken)
    {
        // A transaction the caller opened itself (a fixture, a maintenance routine) already owns
        // the scope's saves, and nesting one inside it is not something a relational provider
        // offers — so the send rides it rather than competing with it.
        if (_transaction is not null || _db.Database.CurrentTransaction is not null)
        {
            return;
        }

        _transaction = await _db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task Commit(CancellationToken cancellationToken)
    {
        if (_transaction is null)
        {
            return;
        }

        var transaction = _transaction;

        _transaction = null;

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        await transaction.DisposeAsync().ConfigureAwait(false);
    }

    public async Task Rollback()
    {
        if (_transaction is null)
        {
            return;
        }

        var transaction = _transaction;

        _transaction = null;

        await transaction.RollbackAsync().ConfigureAwait(false);
        await transaction.DisposeAsync().ConfigureAwait(false);
    }
}
