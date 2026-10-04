// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSail.Background;
using NSail.Messaging;
using NSail.Messaging.Runtime;
using NSail.Security;

namespace NSail.Data.Testing;

public sealed class HandlerHost : IAsyncDisposable
{
    readonly TestDatabase _database;
    readonly ServiceProvider _services;

    HandlerHost(TestDatabase database, ServiceProvider services)
    {
        _database = database;
        _services = services;
    }

    public static async Task<HandlerHost> Start(
        string prefix,
        Func<string, DbContext> context,
        Action<IServiceCollection> compose)
    {
        var database = new TestDatabase(prefix, context);

        await database.Migrate();

        var services = new ServiceCollection();

        services.AddLogging();
        services.AddMessaging();

        // The gate is on, not stubbed. A handler test that reached past the pipeline would
        // prove the handler and imply the surface, and the two are only the same thing while
        // nobody checks — what a Send meets here is what a request meets.
        services.AddSecurityEnforcement();

        // An app host binds a connection string from configuration and reaches its context
        // through AddDataAccess (data.md, App composition). A fixture has no configuration and
        // no tenancy to resolve, so the scoped DbContext handlers inject is built by the same
        // delegate the database was migrated with — one definition of the context, not two.
        //
        // AddDataAccess itself is not callable here: it registers TDbContext from its own
        // AddDbContextFactory delegate, but Start takes a Func<string, DbContext> with no
        // TDbContext to close over, so there is no generic argument to hand it.
        services.AddScoped(_ => context(database.ConnectionString));

        // The rest of what AddDataAccess would have registered, at its own default: a fixture
        // resolves no tenant, and a service that answers Tenant.None is what an install under
        // None answers too. Without it a handler that asks who the tenant is — sign-in, minting
        // the claim it was issued for — is unresolvable in every fixture that composes it.
        services.AddSingleton(new TenancyOptions());
        services.AddScoped<TenancyProvider>();

        // Every branch, at its own default: a fixture composing no Iam resolves no scope, and a
        // handler that reads OrgScopeProvider unconditionally (an in-use guard's ReadEverywhere,
        // data.md — The org filter) is unresolvable without it. A collection that stands a caller
        // in one branch enters its own (OrgScopes.StandIn and its siblings).
        services.TryAddScoped<OrgScopeProvider>();

        // The other half of what SetDefaultDbContext hands a host: the scope's context enlisted
        // in the send's transaction, so a composed flow is as atomic here as it is in production.
        services.AddUnitOfWork();

        compose(services);

        // Last, so it wins over the permissive default AddSecurity TryAdded: the session a
        // test sets has to reach the gate on every send the operation makes, and the operation
        // runs in a scope of its own.
        services.AddScoped<SessionProvider, AmbientSessionProvider>();

        return new HandlerHost(database, services.BuildServiceProvider());
    }

    public async Task<TResult> Send<TResult>(IMessage<TResult> message, Session session)
    {
        await using var scope = _services.CreateAsyncScope();

        scope.ServiceProvider.SetSession(session);

        var result = await scope.ServiceProvider.GetRequiredService<Mediator>().Send(message);

        await DrainDeferredWork();

        return result;
    }

    public async Task Send(IMessage message, Session session)
    {
        await using var scope = _services.CreateAsyncScope();

        scope.ServiceProvider.SetSession(session);

        await scope.ServiceProvider.GetRequiredService<Mediator>().Send(message);

        await DrainDeferredWork();
    }

    /// <summary>Publishes a domain event to every handler the composition registered for it —
    /// the twin of <see cref="Send(IMessage, Session)"/> for a consumer whose whole contract is
    /// the event. A fixture reaching for this is testing the READER of a fact; the writer's own
    /// publish is proven by sending the message that causes it.</summary>
    public async Task Publish(IMessage message, Session session)
    {
        await using var scope = _services.CreateAsyncScope();

        scope.ServiceProvider.SetSession(session);

        await scope.ServiceProvider.GetRequiredService<Mediator>().Publish(message);

        await DrainDeferredWork();
    }

    // A fixture composes DeferredWork only where a kit under test enqueues onto it
    // (AddDeferredWork) — most do not, so this is a no-op there. Where one does, HandlerHost
    // raises no IHost and DeferredWorkRunner never starts, so a Send whose handler fired a
    // notice through the queue must drain it itself or a fixture asserting on that notice
    // would see nothing, forever (WorkOrderNotices.Send is the first caller).
    async Task DrainDeferredWork()
    {
        if (_services.GetService<DeferredWork>() is { } deferred)
        {
            await deferred.DrainAll(_services.GetRequiredService<IServiceScopeFactory>());
        }
    }

    // Its own scope, so what it reads is what the database holds and not what the send's
    // change tracker is still carrying — the difference between asserting a row and asserting
    // the entity the handler just wrote to.
    public async Task<TResult> Read<TResult>(Func<DbContext, Task<TResult>> read)
    {
        await using var scope = _services.CreateAsyncScope();

        return await read(scope.ServiceProvider.GetRequiredService<DbContext>());
    }

    // A composed service reached directly, for the half of a kit that sits behind no message:
    // a provider the framework calls (ISignInProvider), a manager another kit injects. Its
    // own scope, like Read and Seed, so what it touches is the database rather than some
    // other operation's change tracker.
    public async Task<TResult> Use<TService, TResult>(Func<TService, Task<TResult>> use)
        where TService : notnull
    {
        await using var scope = _services.CreateAsyncScope();

        return await use(scope.ServiceProvider.GetRequiredService<TService>());
    }

    // Same as above, for a service whose own method sends through Mediator internally (a
    // handler composing another handler's message, tested below its own Handle) -- without a
    // session the gate meets nobody and every inner Send answers Forbidden.
    public async Task<TResult> Use<TService, TResult>(Func<TService, Task<TResult>> use, Session session)
        where TService : notnull
    {
        await using var scope = _services.CreateAsyncScope();

        scope.ServiceProvider.SetSession(session);

        return await use(scope.ServiceProvider.GetRequiredService<TService>());
    }

    // Reference data (accounts, currencies, fiscal periods, …) a kit's own handlers never
    // create — a fixture writes the row directly when the kit ships no Create{Entity}
    // message for it. Its own scope for the same reason Read has one: a fixture write must
    // not share a change tracker with the Send it sets up for.
    public async Task Seed(Action<DbContext> write)
    {
        await using var scope = _services.CreateAsyncScope();

        var db = scope.ServiceProvider.GetRequiredService<DbContext>();

        write(db);

        await db.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _services.DisposeAsync();
        await _database.DisposeAsync();
    }
}
