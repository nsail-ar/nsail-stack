// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.FileProviders;
using NSail.Configuration;
using NSail.Data;
using NSail.Localization;

namespace NSail.Sample;

public static class Web
{
    public static void AddSampleWebApi(this IServiceCollection services, IConfiguration configuration)
    {
        var sample = configuration.Load<SampleOptions>();

        services.AddSampleData();
        services.AddDataAccess<SampleDbContext>(sample.ConnectionString, configuration.Load<TenancyOptions>());

        // No Iam is composed, so no sign-in scheme exists: the base pipeline still runs
        // UseAuthentication, which needs the services even with nothing to authenticate against.
        services.AddAuthentication();

        services.AddSampleServices();
        services.AddSampleEndpoints();
        services.AddSampleInProcessClients();
        services.AddSampleSecurity();

        services.AddSingleton(configuration.Load<LanguageOptions>());
    }

    // The React build lands beside the host rather than in wwwroot, which belongs to the Blazor
    // client's static assets; every address under its path that is not a file is the React
    // router's own, so it gets index.html. The Blazor fallback keeps everything else.
    public static void MapReactClient(this WebApplication app)
    {
        var root = Path.Combine(app.Environment.ContentRootPath, "react");
        var index = Path.Combine(root, "index.html");

        if (Directory.Exists(root))
        {
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(root),
                RequestPath = ReactClient.Path,
            });
        }

        app.MapFallback($"{ReactClient.Path}/{{*path:nonfile}}", async context =>
        {
            if (!File.Exists(index))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                await context.Response.WriteAsync("The React client is not built: run npm install at the repository root, then build this project.");

                return;
            }

            context.Response.ContentType = "text/html; charset=utf-8";
            await context.Response.SendFileAsync(index);
        });
    }
}
