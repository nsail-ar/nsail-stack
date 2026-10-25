// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Globalization;
using System.Net.Http;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using NSail.Data;
using NSail.Data.Testing;
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

    // Iam's other one, retyped for the same reason: what the pipeline is checked on is
    // Session.TicketEpoch, and the claim behind it is the kit's business.
    const string TicketEpochClaimType = "epoch";

    static readonly string Server = ConnectionStrings.Complete("Host=localhost;Username=postgres;Database=postgres");

    readonly WebApplication _app;
    readonly Severance _severance;
    readonly List<string> _provisioned = [];
    readonly string? _owner;
    readonly bool _ownsOwner;

    int _listening;

    TenancyHost(WebApplication app, Severance severance, string cell, string? owner, bool ownsOwner)
    {
        _app = app;
        _severance = severance;
        _owner = owner;
        _ownsOwner = ownsOwner;
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
    /// the suites that count rows are counting their own. <paramref name="clock"/> is the
    /// install's own clock, which is what makes a pause window and the state memo reachable
    /// without waiting; <paramref name="contact"/> is who its suspended page names;
    /// <paramref name="logs"/> is what the install said on its way up.</summary>
    public static Task<TenancyHost> Start(
        TenancyMode mode,
        bool bootstraps = false,
        string? language = null,
        int epoch = 0,
        TimeProvider? clock = null,
        string? contact = null,
        bool requireWall = false,
        bool bypassable = false,
        bool provisions = true,
        ILoggerProvider? logs = null)
    {
        return Start(
            mode,
            bootstraps ? typeof(WelcomeSeed) : null,
            cell: null,
            language,
            epoch,
            clock,
            contact,
            requireWall,
            bypassable,
            provisions,
            logs);
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

    public static async Task<TenancyHost> Start(
        TenancyMode mode,
        Type? seed,
        string? cell,
        string? language = null,
        int epoch = 0,
        TimeProvider? clock = null,
        string? contact = null,
        bool requireWall = false,
        bool bypassable = false,
        bool provisions = true,
        ILoggerProvider? logs = null)
    {
        var owned = cell is null;

        cell ??= "c" + Guid.NewGuid().ToString("N")[..12];

        var tenancy = new TenancyOptions
        {
            Mode = mode,
            Product = Product,
            Cell = cell,
            SupportContact = contact,
            RequireRowSecurity = requireWall,
        };

        // The app connects as a role that is NEITHER a superuser NOR BYPASSRLS, because a
        // forced policy is no wall against either and every other suite in the house connects
        // as postgres: the behaviour the row policies have is measured here or nowhere. The
        // role OWNS its databases, which is what lets it run the chain — and FORCE ROW LEVEL
        // SECURITY is what subjects an owner to its own tables' policies. Derived from the
        // cell so a redeploy onto the same databases reaches the same role without carrying
        // it, and dropped by whoever minted the cell.
        var owner = bypassable ? null : Role(cell);

        if (owner is not null && owned)
        {
            await Execute(
                $"CREATE ROLE \"{owner}\" LOGIN PASSWORD '{owner}' NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE");
        }

        // Under None the install names a database of its own, exactly as a single-tenant
        // install does today; under MultiDb only the server, user and password are read from
        // it, and the database it names is never opened.
        var install = Connection($"{Product}_{cell}", owner);

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

        // A provider rather than a fake ILogger<T>: what the wall says on its way up is logged
        // on a logger typed on an internal class, so naming it from here would need the module
        // to open itself up for a test.
        if (logs is not null)
        {
            builder.Logging.AddProvider(logs);
        }

        // The install's own language, which is what the one page this pipeline draws itself
        // speaks (TenantStateMiddleware): the gate stands ahead of the step that resolves the
        // reader's, so a suite proving both languages states the install's.
        if (language is not null)
        {
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Language:Default"] = language,
            });
        }

        // The generation of ticket this install honours, stated the one way an install states
        // it: through its own configuration, read by AddBaseWebApi. Zero is every other suite
        // here, and it is what proves the cut costs an install that never raised it nothing.
        if (epoch != 0)
        {
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Security:TicketEpoch"] = epoch.ToString(CultureInfo.InvariantCulture),
            });
        }

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

        // Ahead of AddDataAccess, which registers the machine's clock with TryAdd: a host that
        // states its own keeps it, which is the seam the state memo and the pause window are
        // reached through.
        if (clock is not null)
        {
            builder.Services.AddSingleton(clock);
        }

        builder.Services.AddDataAccess<TenantDbContext>(install, tenancy);

        if (seed is not null)
        {
            builder.Services.TryAddEnumerable(ServiceDescriptor.Scoped(typeof(ITenantSeed), seed));
        }

        PushProbes.Register(builder.Services);

        // The push's lines, so a listener here is waited for on the server's side as it is in
        // PushTests: the handshake a client returns on precedes the audience it joins.
        var severance = new Severance();

        builder.Services.AddSignalR(options => options.AddFilter(severance));

        // What a kit holding a tenant's rows in memory registers, recording instead of holding.
        builder.Services.AddTenantCache<ForgottenTenants>();

        var app = builder.Build();

        var host = new TenancyHost(app, severance, cell, owner, owner is not null && owned);

        // A refusal on the way up is one of the things measured here
        // (Tenancy:RequireRowSecurity), and by then the cell has been minted: without this the
        // databases and the role it created would outlive the run.
        try
        {
            // Every mode but the one that connects per tenant runs on the install's own
            // database, and that database has to exist before the chain is applied to it.
            // SingleDb is here for exactly that reason: many tenants, one database, and it is
            // the install's.
            if (mode != TenancyMode.MultiDb && owned)
            {
                if (provisions)
                {
                    await host.Provision(null);
                }
                else
                {
                    // Claimed and not created: what the startup pass does with a database that
                    // is not there yet is itself a subject, and the run still has to drop
                    // whatever EF made.
                    host.Claim(null);
                }
            }

            await app.ApplyMigrations<TenantDbContext>();

            app.UseBaseWebApi();

            severance.Watch(app);

            Map(app);

            await app.StartAsync();
        }
        catch
        {
            await host.DisposeAsync();

            throw;
        }

        host.Client = app.GetTestClient();
        host.Client.BaseAddress = new Uri("https://demo.nsail.ar/");

        return host;
    }

    public string DatabaseOf(string? slug)
    {
        return slug is null ? $"{Product}_{Cell}" : $"{Product}_{Cell}_{slug}";
    }

    /// <summary>What the provisioning side does and all it does: create the database, owned by
    /// the role the app connects as. No schema — the chain is the first request's to apply, and
    /// the role that applies it is the role that owns what it creates.</summary>
    public async Task Provision(string? slug)
    {
        var database = DatabaseOf(slug);

        var owner = _owner is null ? string.Empty : $" OWNER \"{_owner}\"";

        await Execute($"CREATE DATABASE \"{database}\"{owner}");

        _provisioned.Add(database);
    }

    /// <summary>Records a database this run is responsible for dropping without creating
    /// it.</summary>
    public void Claim(string? slug)
    {
        _provisioned.Add(DatabaseOf(slug));
    }

    /// <summary>The app's own connection string — its role, its install database — which is the
    /// one connection in a run the row policies apply to, and the one the chain runs on.</summary>
    public string InstallConnection
    {
        get { return Connection($"{Product}_{Cell}", _owner); }
    }

    /// <summary>Whether the app's own role could step over the row policies — what the install
    /// asks itself at startup, asked here of the same role over the same connection.</summary>
    public async Task<bool> RoleCanBypass()
    {
        await using var connection = new NpgsqlConnection(InstallConnection);

        await connection.OpenAsync();

        await using var query = new NpgsqlCommand(
            "SELECT rolsuper OR rolbypassrls FROM pg_roles WHERE rolname = current_user",
            connection);

        return await query.ExecuteScalarAsync() is true;
    }

    static string Role(string cell)
    {
        return $"r_{cell}";
    }

    // Built off the admin string rather than written out, so the host and port a containerized
    // run repoints (PGHOST, PGPORT) reach the app's own connection too: ConnectionStrings.Complete
    // leaves a string that already carries a password untouched, and the role's does.
    static string Connection(string database, string? role)
    {
        // Capped for the reason TestDatabase caps its own: every host here keys a pool of its
        // own, and a suite that raises several of them at once would otherwise be able to reach
        // the one shared server's connection ceiling by itself.
        var builder = new NpgsqlConnectionStringBuilder(Server) { Database = database, MaxPoolSize = 10 };

        if (role is not null)
        {
            builder.Username = role;
            builder.Password = role;
        }

        return builder.ConnectionString;
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
    /// <summary>A signed-in client of this tenant listening over this transport, handed back
    /// once the SERVER holds its line — see <see cref="Severance"/>.</summary>
    internal async Task<PushClient> Listen(Transport transport, string tenant)
    {
        var client = await PushClient.Listen(Pipeline, transport, await SignIn(tenant), tenant);

        await _severance.Held(lines: ++_listening);

        return client;
    }

    public async Task<string> SignIn(string? tenant, bool stamp = true, int? epoch = null)
    {
        var generation = epoch is { } stated ? $"&epoch={stated}" : string.Empty;

        using var response = await Client.SendAsync(Get($"/sign-in?stamp={stamp}{generation}", tenant));

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

        if (_ownsOwner)
        {
            await Execute($"DROP ROLE IF EXISTS \"{_owner}\"");
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

        // What the install records that this tenant is entitled to, read the way the gate reads
        // it — through the composed FeatureProvider and not off the row — so the registration,
        // the jsonb column's round trip and the permissive parse are one answer.
        app.MapGet("/entitlements", async (FeatureProvider features) =>
        {
            var entitlements = await features.Current();

            return Results.Json(new Bag(entitlements.IsEnabled("whatsapp"), entitlements.Limit("organizations.max")));
        });

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

        // The query that BYPASSES the first wall, which is the whole subject of the second one:
        // raw SQL carries no query filter, so what comes back is what Postgres decided to show
        // this connection. IgnoreQueryFilters is refused everywhere in the house, and this is
        // the stronger form of the same attempt anyway — the filter is not merely skipped, it
        // was never compiled in.
        app.MapGet("/notes/unfiltered", async (DbContext db) =>
        {
            return Results.Json(await db.Database
                .SqlQueryRaw<string>("SELECT \"Text\" AS \"Value\" FROM \"Notes\" ORDER BY \"Text\"")
                .ToListAsync());
        });

        // And the write that bypasses the stamp: a row naming the tenant it would like to belong
        // to, in SQL, where no interceptor stands between the caller and the table.
        app.MapPost("/notes/unfiltered", async (string text, Guid tenant, DbContext db) =>
        {
            await db.Database.ExecuteSqlRawAsync(
                "INSERT INTO \"Notes\" (\"Id\", \"TenantId\", \"Text\") VALUES (gen_random_uuid(), {0}, {1})",
                tenant,
                text);

            return Results.Ok();
        });

        // What the chain itself wrote, read the same unfiltered way: a data migration's INSERT
        // into a tenant-scoped table, which is the install's own row and nobody's tenant.
        app.MapGet("/journals/unfiltered", async (DbContext db) =>
        {
            return Results.Json(await db.Database
                .SqlQueryRaw<string>("SELECT \"Text\" AS \"Value\" FROM \"Journals\" ORDER BY \"Text\"")
                .ToListAsync());
        });

        // The sweep a product's own suite runs over its chain, run here over this one.
        app.MapGet("/unwalled", async (DbContext db) =>
        {
            return Results.Json(await TenantPolicyAudit.Unwalled(db));
        });

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
        //
        // The generation comes from the install's own options for the same reason, so a plain
        // sign-in here is the one a person makes after the cut. `epoch` overrides it, which is
        // the only way to hold a ticket from before: zero mints no claim at all — the ticket
        // from before the cut existed — and any other number is one generation behind or ahead.
        app.MapGet("/sign-in", async (HttpContext context, TenancyProvider tenancy, SecurityOptions security, bool stamp, int? epoch) =>
        {
            var claims = new List<Claim>();

            if (stamp && tenancy.Current is { IsResolved: true, Slug: { } slug })
            {
                claims.Add(new Claim(TenantClaimType, slug));
            }

            if ((epoch ?? security.TicketEpoch) is var stamped and not 0)
            {
                claims.Add(new Claim(TicketEpochClaimType, stamped.ToString(CultureInfo.InvariantCulture)));
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
                    TicketEpoch = int.TryParse(
                        user.FindFirst(TicketEpochClaimType)?.Value,
                        CultureInfo.InvariantCulture,
                        out var epoch)
                        ? epoch
                        : 0,
                };
            }

            set { base.Session = value; }
        }
    }
}

public sealed record Where(string? Slug, string? Database, string Connection);

/// <summary>The two worked keys of the entitlements bag, as the gate's own reader answers them:
/// a flag and a ceiling, each at its permissive default where the bag says nothing.</summary>
public sealed record Bag(bool WhatsApp, decimal? Organizations);
