// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using NSail.Data;

namespace NSail.Sample;

public class SampleDbContextFactory : IDesignTimeDbContextFactory<SampleDbContext>
{
    const string DefaultConnectionString =
        "Host=localhost;Database=sample_main;Username=postgres";

    public SampleDbContext CreateDbContext(string[] args)
    {
        // Design time never builds the host, so appsettings.json is out of reach here: the
        // connection string has to arrive on the command line or the environment.
        var configuration = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .AddCommandLine(args)
            .Build();

        var connectionString = configuration["Sample:ConnectionString"];

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = DefaultConnectionString;
        }

        var optionsBuilder = new DbContextOptionsBuilder<SampleDbContext>();
        optionsBuilder.UseNpgsql(ConnectionStrings.Complete(connectionString));

        return new SampleDbContext(optionsBuilder.Options, [new DbContextSetup()]);
    }
}
