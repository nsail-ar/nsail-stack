// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Net.Http;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Npgsql;
using NSail.Data;
using NSail.Security;

namespace NSail.BaseServices.WebApi.Tests;

/// <summary>A real host behind the real pipeline: <c>AddBaseWebApi</c>, <c>AddDataAccess</c> and
/// <c>UseBaseWebApi</c> as a product calls them, over the slot's own Postgres. The order the
/// tenancy answer lands in is the whole subject, so the pipeline is called rather than
/// rebuilt.</summary>
public sealed class TenancyHost : IAsyncDisposable
{
    // Every run mints its own cell, so two slots on the shared server never provision each
    // other's tenants — and the product/cell half of the name is exercised for real.
    public const string Product = "nsail357";

    // Iam's own claim type, retyped rather than referenced: a Stack test that took a kit
    // reference to read one string would point the dependency arrow the wrong way. What the
    // pipeline is checked on is Session.Tenant, and the claim behind it is Iam's business.
    const string TenantClaimType = "tenant";

    static readonly string Server = ConnectionStrings.Complete("Host=localhost;Username=postgres;Database=postgres");

    readonly WebApplication _app;
    readonly List<string> _provisioned = [];

    TenancyHost(WebApplication app, string cell)
    {
        _app = app;
        Cell = cell;
    }

    public string Cell { get; }

    public HttpClient Client { get; private set; } = null!;

    // The in-memory server itself, for a client that brings its own handler — the push's.
    public TestServer Pipeline
    {
        get { return _app.GetTestServer(); }
    }

    // The container itself, because the scope that resolves no tenant cannot be reached over
    // HTTP: the middleware answers 404 to an unresolved slug before any endpoint runs. Work that
    // is not a request — a background job, a startup task — opens its scope exactly like this.
    public IServiceProvider Services
    {
        get { return _app.Services; }
    }

    /// <summary>The host every tenancy suite runs against. <paramref name="bootstraps"/> adds
    /// what an app would register — the rows a tenant is born with — and is off by default so
    /// the suites that count rows are counting their own.</summary>
    public static Task<TenancyHost> Start(TenancyMode mode, bool bootstraps = false)
    {
        return Start(mode, bootstraps ? typeof(WelcomeSeed) : null, cell: null);
    }

    /// <summary>The same install again, carrying whatever chain that build carries — a deploy,
    /// in the only shape a test can have one: another process, another <c>FirstTouch</c> memo,
    /// the same database. It provisions nothing and drops nothing: the database is the host it
    /// was started beside.</summary>
    public static Task<TenancyHost> Redeploy(TenancyHost host, Type seed)
    {
        ArgumentNullException.ThrowIfNull(host);

        return Start(TenancyMode.SingleDb, seed, host.Cell);
    }

    public static async Task<TenancyHost> Start(TenancyMode mode, Type? seed, string? cell)
    {
        var owned = cell is null;

        cell ??= "c" + Guid.NewGuid().ToString("N")[..12];
        var tenancy = new TenancyOptions { Mode = mode, Product = Product, Cell = cell };

        // Under None the install names a database of its own, exactly as a single-tenant
        // install does today; under MultiDb only the server, user and password are read from
        // it, and the database it names is never opened.
        var install = ConnectionStrings.Complete($"Host=localhost;Username=postgres;Database={Product}_{cell}");

        // Development, because that is what turns on the container's own build-time validation
        // — WebApplicationBuilder keys ValidateOnBuild to the environment and exposes no other
        // knob. Without it a descriptor no mode can construct is invisible here and crashes the
        // first `dotnet run` instead: the pipeline is registered by every host and installed by
        // only some, and that asymmetry is exactly what validation catches.
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Development,
        });

        builder.WebHost.UseTestServer();
        builder.AddBaseWebApi();

        // Cookies, because the subject is a ticket that outlives the request that minted it and
        // can be carried to another host — the only kind of credential that can cross a tenant
        // wall at all. A caller with none earns a 401 rather than the handler's own redirect,
        // which is what the tenancy answer has to arrive before.
        builder.Services
            .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.Events.OnRedirectToLogin = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;

                    return Task.CompletedTask;
                };
            });

        builder.Services.AddAuthorization();
        builder.Services.AddHttpContextAccessor();

        // Iam's own adapter in miniature: the Stack's wall reads Session, never a claim, so what
        // the pipeline needs here is something that turns this host's principal into one. That
        // the real adapter reads the real claim is Iam's to prove, and its tests do.
        builder.Services.AddScoped<SessionProvider, TicketSessionProvider>();
        builder.Services.AddDataAccess<TenantDbContext>(install, tenancy);

        if (seed is not null)
        {
            builder.Services.TryAddEnumerable(ServiceDescriptor.Scoped(typeof(ITenantSeed), seed));
        }

        PushProbes.Register(builder.Services);

        // What a kit holding a tenant's rows in memory registers, recording instead of holding.
        builder.Services.AddTenantCache<ForgottenTenants>();

        var app = builder.Build();

        var host = new TenancyHost(app, cell);

        // Every mode but the one that connects per tenant runs on the install's own database,
        // and that database has to exist before the chain is applied to it. SingleDb is here
        // for exactly that reason: many tenants, one database, and it is the install's.
        if (mode != TenancyMode.MultiDb && owned)
        {
            await host.Provision(null);
        }

        await app.ApplyMigrations<TenantDbContext>();

        app.UseBaseWebApi();

        Map(app);

        await app.StartAsync();

        host.Client = app.GetTestClient();
        host.Client.BaseAddress = new Uri("https://demo.nsail.ar/");

        return host;
    }

    public string DatabaseOf(string? slug)
    {
        return slug is null ? $"{Product}_{Cell}" : $"{Product}_{Cell}_{slug}";
    }

    /// <summary>What the provisioning side does and all it does: create the database. No
    /// schema — the chain is the first request's to apply.</summary>
    public async Task Provision(string? slug)
    {
        var database = DatabaseOf(slug);

        await Execute($"CREATE DATABASE \"{database}\"");

        _provisioned.Add(database);
    }

    public async Task<bool> Exists(string? slug)
    {
        await using var admin = new NpgsqlConnection(Server);

        await admin.OpenAsync();

        await using var query = new NpgsqlCommand("SELECT 1 FROM pg_database WHERE datname = @name", admin);

        query.Parameters.AddWithValue("name", DatabaseOf(slug));

        return await query.ExecuteScalarAsync() is not null;
    }

    public async Task<IReadOnlyList<string>> AppliedMigrations(string? slug)
    {
        await using var tenant = new NpgsqlConnection(
            ConnectionStrings.Complete($"Host=localhost;Username=postgres;Database={DatabaseOf(slug)}"));

        await tenant.OpenAsync();

        // Asked separately: a statement naming a table that does not exist fails when the
        // server plans it, so the guard cannot ride in the same query. Before the first touch
        // there is no history table at all, and "nothing applied" is the honest reading.
        await using (var exists = new NpgsqlCommand("SELECT to_regclass('public.\"__EFMigrationsHistory\"')::text", tenant))
        {
            if (await exists.ExecuteScalarAsync() is null or DBNull)
            {
                return [];
            }
        }

        await using var query = new NpgsqlCommand(
            "SELECT \"MigrationId\" FROM \"__EFMigrationsHistory\" ORDER BY \"MigrationId\"",
            tenant);

        var applied = new List<string>();

        await using var reader = await query.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            applied.Add(reader.GetString(0));
        }

        return applied;
    }

    public HttpRequestMessage Get(string path, string? tenant, string host = "demo.nsail.ar", string? ticket = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, new Uri($"https://{host}{path}"));

        if (tenant is not null)
        {
            request.Headers.Add(TenancyMiddleware.TenantHeader, tenant);
        }

        if (ticket is not null)
        {
            request.Headers.Add("Cookie", ticket);
        }

        return request;
    }

    /// <summary>Signs in through the pipeline and hands back the cookie itself, which is what
    /// makes a ticket carryable: the test client keeps no jar, so a credential only reaches a
    /// second request — or a second tenant's host — because somebody carried it there.</summary>
    public async Task<string> SignIn(string? tenant, bool stamp = true)
    {
        using var response = await Client.SendAsync(Get($"/sign-in?stamp={stamp}", tenant));

        response.EnsureSuccessStatusCode();

        return response.Headers.GetValues("Set-Cookie").Single().Split(';')[0];
    }

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();

        NpgsqlConnection.ClearAllPools();

        foreach (var database in _provisioned)
        {
            await Execute($"DROP DATABASE IF EXISTS \"{database}\" WITH (FORCE)");
        }
    }

    static void Map(WebApplication app)
    {
        // The database the scope's own context is connected to, read off the connection rather
        // than off the tenant that was resolved: what is asserted is where the work would run.
        app.MapGet("/where", (TenancyProvider tenancy, DbContext db) => Results.Json(new Where(
            tenancy.Current.Slug,
            tenancy.Current.Database,
            db.Database.GetDbConnection().Database)));

        // The same answer from a scope the request opened for itself: a prerendered component
        // resolving a brand takes one, so a tenant that only reached the request's own scope
        // would 500 on a page that had already been answered 200 for.
        app.MapGet("/where-under-a-scope-of-its-own", async (IServiceScopeFactory scopes) =>
        {
            await using var opened = scopes.CreateAsyncScope();

            var tenancy = opened.ServiceProvider.GetRequiredService<TenancyProvider>();

            return Results.Json(new Where(
                tenancy.Current.Slug,
                tenancy.Current.Database,
                opened.ServiceProvider.GetRequiredService<DbContext>().Database.GetDbConnection().Database));
        });

        app.MapPost("/notes", async (string text, DbContext db) =>
        {
            db.Add(new Note { Id = Guid.NewGuid(), Text = text });

            await db.SaveChangesAsync();

            return Results.Ok();
        });

        app.MapGet("/notes", async (DbContext db) =>
        {
            return Results.Json(await db.Set<Note>().Select(note => note.Text).OrderBy(text => text).ToListAsync());
        });

        app.MapGet("/kinds", async (DbContext db) =>
        {
            return Results.Json(await db.Set<NoteKind>().Select(kind => kind.Name).OrderBy(name => name).ToListAsync());
        });

        // A caller naming the tenant it would like its row to belong to, in the only place a
        // caller could reach it at all: the tracker, one line before the save. Nothing on the
        // wire can name a shadow property, so this is the strongest form the attempt has.
        app.MapPost("/notes/forged", async (string text, Guid tenant, DbContext db) =>
        {
            var note = new Note { Id = Guid.NewGuid(), Text = text };

            db.Add(note);
            db.Entry(note).Property("TenantId").CurrentValue = tenant;

            await db.SaveChangesAsync();

            return Results.Ok();
        });

        app.MapGet("/guarded", () => Results.Ok()).RequireAuthorization();

        // The push's own publishes (PushTenancyTests), made through the request's Mediator like
        // any handler's, so the audience is whichever tenant the edge resolved.
        PushProbes.MapPublishes(app);

        // The one arm that reaches ErrorMiddleware's own rewrite: it clears the response before
        // writing its Problem, so whatever the pipeline put on the way in is gone by the time
        // the answer leaves.
        app.MapGet("/boom", IResult () =>
        {
            throw new KeyNotFoundException();
        });

        // A sign-in in miniature: the ticket carries the tenant the work that issued it was
        // running on — what PrincipalFactory stamps — and an install that resolved none carries
        // nothing. `stamp: false` is the ticket minted before the wall existed.
        app.MapGet("/sign-in", async (HttpContext context, TenancyProvider tenancy, bool stamp) =>
        {
            var claims = new List<Claim>();

            if (stamp && tenancy.Current is { IsResolved: true, Slug: { } slug })
            {
                claims.Add(new Claim(TenantClaimType, slug));
            }

            await context.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)));

            return Results.Ok();
        });
    }

    static async Task Execute(string statement)
    {
        await using var admin = new NpgsqlConnection(Server);

        await admin.OpenAsync();

        await using var command = new NpgsqlCommand(statement, admin);

        await command.ExecuteNonQueryAsync();
    }

    sealed class TicketSessionProvider : SessionProvider
    {
        readonly IHttpContextAccessor _httpContextAccessor;

        public TicketSessionProvider(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public override Session Session
        {
            get
            {
                if (_httpContextAccessor.HttpContext?.User is not { Identity.IsAuthenticated: true } user)
                {
                    return new Session();
                }

                return new Session
                {
                    IsAuthenticated = true,
                    Tenant = user.FindFirst(TenantClaimType)?.Value,
                };
            }

            set { base.Session = value; }
        }
    }
}

public sealed record Where(string? Slug, string? Database, string Connection);
