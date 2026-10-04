// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.DependencyInjection;
using NSail.Problems;

namespace NSail.Messaging.Runtime.UnitOfWork;

// The outermost in-process send owns the scope and the transaction; every send nested inside it
// joins both. Before this, each nested Mediator.Send took a scope — and therefore a DbContext and
// a connection — of its own and committed alone, so a composing handler whose step 2 failed left
// step 1 standing: a stock movement with no sale behind it, a delivered order whose delivery was
// refused, a credit note for a return that never happened.
//
// The ambient travels by AsyncLocal because that is the only channel a handler cannot forget to
// pass: composing handlers send exactly as they did, and the cure lives once, here.
//
// It is deliberately not reachable from a handler: there is no ambient to inspect, no way to
// suppress it and no escape hatch, until a real case asks for one.
static class AmbientUnitOfWork
{
    static readonly AsyncLocal<Unit?> Current = new();

    // How far inside the ambient this async flow is. Two sends started in parallel from one
    // handler read the same value and so collide on the same key — which is how the guard below
    // tells concurrency apart from nesting, a distinction a plain counter cannot make.
    static readonly AsyncLocal<int> Depth = new();

    public static Task Run(
        IServiceScopeFactory scopeFactory,
        string messageType,
        Func<IServiceProvider, CancellationToken, Task> handle,
        CancellationToken cancellationToken)
    {
        return Run<object?>(
            scopeFactory,
            messageType,
            async (services, token) =>
            {
                await handle(services, token).ConfigureAwait(false);
                return null;
            },
            cancellationToken);
    }

    public static async Task<TResult> Run<TResult>(
        IServiceScopeFactory scopeFactory,
        string messageType,
        Func<IServiceProvider, CancellationToken, Task<TResult>> handle,
        CancellationToken cancellationToken)
    {
        if (Current.Value is { } ambient)
        {
            return await Join(ambient, messageType, handle, cancellationToken).ConfigureAwait(false);
        }

        await using var scope = scopeFactory.CreateAsyncScope();

        var unit = new Unit(scope.ServiceProvider);

        try
        {
            // Opened here rather than at the first write, because the runtime has no honest
            // signal for "the first write": the candidates are an EF interceptor every host would
            // have to remember to wire (and would lose atomicity by forgetting) and a guess from
            // the message's shape (which a command that returns a result would silently fall
            // outside of). A read-only send pays one BEGIN and one COMMIT on a connection it was
            // going to open anyway — the accepted cost of the ratified design.
            await unit.Begin(cancellationToken).ConfigureAwait(false);

            var result = await Invoke(unit, 1, () => handle(scope.ServiceProvider, cancellationToken))
                .ConfigureAwait(false);

            await unit.Commit(cancellationToken).ConfigureAwait(false);

            return result;
        }
        catch
        {
            await unit.Rollback().ConfigureAwait(false);
            throw;
        }
    }

    static async Task<TResult> Join<TResult>(
        Unit ambient,
        string messageType,
        Func<IServiceProvider, CancellationToken, Task<TResult>> handle,
        CancellationToken cancellationToken)
    {
        var depth = Depth.Value;

        ambient.Enter(depth, messageType);

        try
        {
            return await Invoke(ambient, depth + 1, () => handle(ambient.Services, cancellationToken))
                .ConfigureAwait(false);
        }
        finally
        {
            ambient.Leave(depth);
        }
    }

    // The ambient is written, the handler is STARTED, and the write is undone before this method
    // ever awaits. An AsyncLocal written inside an async method reaches the handler's own flow (it
    // is captured at the handler's first await) but also leaks back to the caller for as long as
    // the caller has not awaited — and a caller that starts two sends before awaiting either is
    // exactly the case the depth key exists to catch. Undoing the write at once is what keeps the
    // second send reading the depth it was really started at.
    static Task<TResult> Invoke<TResult>(Unit? unit, int depth, Func<Task<TResult>> start)
    {
        var previousUnit = Current.Value;
        var previousDepth = Depth.Value;

        Current.Value = unit;
        Depth.Value = depth;

        try
        {
            return start();
        }
        finally
        {
            Current.Value = previousUnit;
            Depth.Value = previousDepth;
        }
    }

    sealed class Unit
    {
        readonly IUnitOfWork[] _stores;
        readonly HashSet<int> _active = [];
        readonly object _gate = new();

        public Unit(IServiceProvider services)
        {
            Services = services;
            _stores = services.GetServices<IUnitOfWork>().ToArray();
        }

        public IServiceProvider Services { get; }

        // One scope means one DbContext and one connection, and neither survives two operations
        // at once. Sends run one after another inside a handler, so a second one arriving at the
        // same depth means the flow forked — refused by name here rather than left to surface as
        // a torn transaction or as EF's own complaint about a context already in use.
        public void Enter(int depth, string messageType)
        {
            lock (_gate)
            {
                if (!_active.Add(depth))
                {
                    throw new BusinessException(MessagingProblem.ConcurrentSend(messageType));
                }
            }
        }

        public void Leave(int depth)
        {
            lock (_gate)
            {
                _active.Remove(depth);
            }
        }

        public async Task Begin(CancellationToken cancellationToken)
        {
            foreach (var store in _stores)
            {
                await store.Begin(cancellationToken).ConfigureAwait(false);
            }
        }

        public async Task Commit(CancellationToken cancellationToken)
        {
            foreach (var store in _stores)
            {
                await store.Commit(cancellationToken).ConfigureAwait(false);
            }
        }

        public async Task Rollback()
        {
            foreach (var store in _stores)
            {
                try
                {
                    await store.Rollback().ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // The failure that brought the flow here is the one the caller has to read. A
                    // store whose undo also fails has lost its connection, which undoes the work
                    // anyway; rethrowing would replace the cause with its consequence.
                }
            }
        }
    }
}
