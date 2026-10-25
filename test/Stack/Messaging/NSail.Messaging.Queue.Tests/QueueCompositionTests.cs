// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSail.Data;

namespace NSail.Messaging.Queue.Tests;

/// <summary>nsail#1930, the fifth criterion: with no configuration the install queues on its
/// own database and deploys nothing it does not deploy today, and naming a broker is refused
/// at startup, by name, until the brokers' own story lands.</summary>
public sealed class QueueCompositionTests
{
    static IHost Host(params (string Key, string Value)[] settings)
    {
        var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder();

        builder.Configuration.AddInMemoryCollection(settings.Select(setting => new KeyValuePair<string, string?>(setting.Key, setting.Value)));

        builder.Services.AddQueue();

        return builder.Build();
    }

    [Theory]
    [InlineData("RabbitMq")]
    [InlineData("AmazonSqs")]
    [InlineData("AzureServiceBus")]
    [InlineData("Postgres")]
    public async Task Naming_a_broker_refuses_at_startup_by_name(string transport)
    {
        using var host = Host(("Queue:Transport", transport));

        var refusal = await Assert.ThrowsAsync<InvalidOperationException>(() => host.StartAsync(CancellationToken.None));

        Assert.Contains(transport, refusal.Message, StringComparison.Ordinal);
        Assert.Contains("InProcess", refusal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_transport_the_install_does_not_know_is_refused_too()
    {
        using var host = Host(("Queue:Transport", "Kafka"));

        var refusal = await Assert.ThrowsAsync<InvalidOperationException>(() => host.StartAsync(CancellationToken.None));

        Assert.Contains("Kafka", refusal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task With_no_configuration_the_host_starts_and_provisions_nothing()
    {
        using var host = Host();

        await host.StartAsync(CancellationToken.None);
        await host.StopAsync(CancellationToken.None);

        var options = host.Services.GetRequiredService<QueueOptions>();

        Assert.Null(options.Transport);

        // Nothing is deployed: the whole of what the queue adds to an install is one slice of
        // the model, in the database the install already has.
        Assert.Single(host.Services.GetServices<IDbContextSetup>().OfType<QueueDbContextSetup>());
    }

    [Fact]
    public async Task Naming_the_in_process_transport_is_what_naming_nothing_already_means()
    {
        using var host = Host(("Queue:Transport", "InProcess"));

        await host.StartAsync(CancellationToken.None);
        await host.StopAsync(CancellationToken.None);
    }

    /// <summary>The figures the delivery loop is judged by are the install's, read off the
    /// section and not written into the loop.</summary>
    [Fact]
    public void The_retry_backoff_and_dead_letter_numbers_come_from_configuration()
    {
        using var host = Host(
            ("Queue:MaxAttempts", "9"),
            ("Queue:Backoff", "00:02:30"),
            ("Queue:Interval", "00:00:05"),
            ("Queue:BatchSize", "7"));

        var options = host.Services.GetRequiredService<QueueOptions>();

        Assert.Equal(9, options.MaxAttempts);
        Assert.Equal(TimeSpan.FromMinutes(2.5), options.Backoff);
        Assert.Equal(TimeSpan.FromSeconds(5), options.Interval);
        Assert.Equal(7, options.BatchSize);
    }
}
