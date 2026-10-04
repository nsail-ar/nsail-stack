// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.OpenApi;
using NSail.Backup;
using NSail.Data;
using NSail.Messaging.Runtime;
using NSail.Messaging.WebApi.Push;
using NSail.Messaging.WebApi;
using NSail.Metadata;
using NSail.Telemetry;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace NSail.BaseServices.WebApi;

public static class Setup
{
    public static void AddBaseWebApi(this WebApplicationBuilder builder)
    {
        // First, so the Mediator span is the outermost interceptor: a kit's
        // AddSecurityEnforcement registers after this, and SendPipeline chains in
        // registration order, so a refusal falls inside the span instead of before it.
        builder.AddTelemetry();

        builder.Services.AddMessaging();
        builder.Services.AddMessagingJson();
        builder.Services.AddErrorHandler();
        builder.Services.AddScoped<EndpointGateMiddleware>();

        // Singleton, and that is the whole of what it is: the counters ARE the middleware, so
        // one built per request would count to one and cap nothing.
        builder.Services.AddSingleton<ThrottleMiddleware>();

        builder.Services.AddSingleton<BuildVersionMiddleware>();

        builder.Services.AddScoped<TenancyMiddleware>();
        builder.Services.AddScoped<TenantClaimMiddleware>();
        builder.Services.AddScoped<CredentialMiddleware>();

        // Ahead of AddPush's own TryAdd: a push reaches the tenant it was published in, and a
        // connection joins the tenant the edge resolved for it.
        builder.Services.AddScoped<PushAudience, TenantPushAudience>();
        builder.Services.AddPush();

        // Not left at the framework default: AddRouting keys ThrowOnBadRequest to
        // IsDevelopment(), so outside Development a binding failure never becomes a
        // BadHttpRequestException — RequestDelegateFactory answers a bare, unlogged 400 and
        // ErrorMiddleware's mapped arm is dead code. Set unconditionally, the exception path
        // is the same one in every environment.
        builder.Services.Configure<RouteHandlerOptions>(options =>
        {
            options.ThrowOnBadRequest = true;
        });

        // The key ring's home is the install's own, not the framework default: the default is a
        // machine-dependent profile folder nothing can package, so an install restored elsewhere
        // would come back with every sealed secret unreadable. Under the content root it travels
        // in the backup archive (KeyRing).
        builder.Services
            .AddDataProtection()
            .PersistKeysToFileSystem(KeyRing.Home(builder.Environment.ContentRootPath));

        builder.Services.AddAuthorization();

        builder.Services.AddMetadata();

        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "NSail API",
                Version = "v1",
                Description = "Generated endpoints"
            });
        });

        // Configured with its dependency instead of inside the delegate above: AddSwaggerGen's
        // delegate is handed the options only, so the provider it needs would have to be a new
        // one — and a host's own MetadataProvider policy is exactly what the id must follow.
        builder.Services
            .AddOptions<SwaggerGenOptions>()
            .Configure<MetadataProvider>((options, metadata) =>
            {
                var resolver = new SchemaIdResolver(metadata);

                options.CustomSchemaIds(resolver.For);
            });
    }

    public static void UseBaseWebApi(this WebApplication app)
    {
        if (app.Configuration.GetValue("ASPNETCORE_FORWARDEDHEADERS_ENABLED", false))
        {
            var forwardedHeadersOptions = new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
            };

            forwardedHeadersOptions.KnownIPNetworks.Clear();
            forwardedHeadersOptions.KnownProxies.Clear();

            app.UseForwardedHeaders(forwardedHeadersOptions);
        }

        // Outermost, so the build that answered rides on every answer this host gives — a
        // refusal, a 404 and a redirect included. It only registers a callback, so nothing
        // below it can drop the stamp by rewriting the response.
        app.UseMiddleware<BuildVersionMiddleware>();

        app.UseErrorHandler();
        app.UseHttpsRedirection();

        // Before authentication, and before anything a tenant's own rows could answer: an
        // install that does not exist must not reach a cookie, a session or an endpoint. Only
        // where a tenant is resolved at all — under None the header is never read, so the
        // pipeline is the one that shipped before the seam existed.
        if (app.ResolvesTenants())
        {
            app.UseMiddleware<TenancyMiddleware>();
        }

        app.UseAuthentication();
        app.UseAuthorization();

        // After authentication, because a claim only exists once the ticket has been read, and
        // before every endpoint, so a credential issued for another tenant is refused ahead of
        // the page, the handler and the circuit alike. Under None it is not installed at all —
        // and where the wall is the tenant column it matters most, because there the connection
        // is not a wall at all.
        if (app.ResolvesTenants())
        {
            app.UseMiddleware<TenantClaimMiddleware>();
        }

        // In front of the gate, because volume is not a permission question: a door open to
        // the whole internet is refused for asking too often before anybody spends a policy
        // evaluation on it. Like the gate, it reads the message off the matched route, so it
        // needs routing to have run and nothing else.
        app.UseMiddleware<ThrottleMiddleware>();

        // Behind the throttle, because asking the database whether a ticket still names anybody
        // costs a query and a flood must be refused before it buys one; ahead of the gate and of
        // every endpoint, because a credential naming a party that is gone must be refused
        // before a handler writes that dead id into a row — and because the gate, the page and
        // the circuit all read the session this step may have re-issued. Installed in every
        // host: a composition that registered no identity keeps the permissive floor and this
        // passes through.
        app.UseMiddleware<CredentialMiddleware>();

        // After authentication, so the session is the one the pipeline's own gate will read,
        // and ahead of MapEndpoints, so it answers before a parameter is bound.
        app.UseMiddleware<EndpointGateMiddleware>();

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }
        else
        {
            app.UseHsts();
        }

        app.MapEndpoints();

        // Mapped here and not by the app, so every host that publishes also has the door its
        // clients listen on — behind the same tenant wall and authentication as every endpoint.
        app.MapPush();
    }
}