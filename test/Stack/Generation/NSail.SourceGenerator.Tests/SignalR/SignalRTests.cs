// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NSail.Messaging;
using NSail.Messaging.Annotations;
using NSail.Messaging.Runtime;
using NSail.Messaging.WebApi.Push;
using NSail.SourceGeneration.Annotations;
using NSail.SourceGeneration.Testing;
using NSail.SourceGenerator.Generation.SignalR;
using System.Collections.Immutable;
using System.Linq;

namespace NSail.SourceGenerator.Tests.SignalR;

/// <summary>nsail#1480: the push's two ends are generated from one walk, so what the hub sends
/// is exactly what a client can name. Each test compiles what it generates against the real
/// Stack types — a registration that named a missing publisher or entry would be an error
/// here, not a push that silently never arrives.</summary>
public class SignalRTests
{
    const string Messages = """
        using Microsoft.Extensions.DependencyInjection;
        using NSail.Messaging;
        using NSail.Messaging.Annotations;
        using NSail.SourceGeneration.Annotations;

        namespace Acme.Inbox;

        [Pushed]
        public class InboxMoved : IMessage
        {
        }

        public class InboxRead : IMessage
        {
        }
        """;

    [Fact]
    public void TheHubRegistersAPublisherForEveryPushedMessageAndNothingElse()
    {
        var generated = Generate(new SignalRHubsSourceGenerator(), """
            public static partial class Hubs
            {
                [Generated(SignalR.Hubs)]
                public static partial void AddInboxHubs(this IServiceCollection services);
            }
            """);

        Assert.Contains(
            "services.AddScoped<global::NSail.Messaging.Runtime.Publishing.IPublisher<global::Acme.Inbox.InboxMoved>, global::NSail.Messaging.WebApi.Push.PushPublisher<global::Acme.Inbox.InboxMoved>>();",
            generated);

        // Unmarked, it stays on the server.
        Assert.DoesNotContain("InboxRead", generated);
    }

    [Fact]
    public void TheClientRegistersAnEntryForTheSameSet()
    {
        var generated = Generate(new SignalRClientsSourceGenerator(), """
            public static partial class Clients
            {
                [Generated(SignalR.Clients)]
                public static partial void AddInboxSignalRClients(this IServiceCollection services);
            }
            """);

        Assert.Contains(
            "services.AddSingleton<global::NSail.Messaging.Runtime.Publishing.PushedMessage>(new global::NSail.Messaging.Runtime.Publishing.PushedMessage<global::Acme.Inbox.InboxMoved>());",
            generated);

        Assert.DoesNotContain("InboxRead", generated);
    }

    // Reported, never skipped: a type left out of the walk is a push that silently never
    // arrives, and generation.md says a generator that cannot honour a declaration says so.
    [Fact]
    public void PushedOnATypeThatIsNotAMessageIsABuildError()
    {
        var diagnostics = Diagnose(new SignalRHubsSourceGenerator(), """
            [Pushed]
            public class NotAMessage
            {
            }

            public static partial class Hubs
            {
                [Generated(SignalR.Hubs)]
                public static partial void AddInboxHubs(this IServiceCollection services);
            }
            """);

        var reported = Assert.Single(diagnostics.Where(d => d.Id == "NSG002"));

        Assert.Equal(DiagnosticSeverity.Error, reported.Severity);
        Assert.Contains("Acme.Inbox.NotAMessage", reported.GetMessage());
    }

    // The body goes to every connection of the tenant, whatever branch its seat stands in: a
    // property on a pushed event is a row crossing the org wall over the socket.
    [Fact]
    public void PushedEventWithAPropertyIsABuildError()
    {
        var diagnostics = Diagnose(new SignalRClientsSourceGenerator(), """
            [Pushed]
            public class InboxRowMoved : IMessage
            {
                public System.Guid ConversationId { get; set; }
            }

            public static partial class Clients
            {
                [Generated(SignalR.Clients)]
                public static partial void AddInboxSignalRClients(this IServiceCollection services);
            }
            """);

        var reported = Assert.Single(diagnostics.Where(d => d.Id == "NSG003"));

        Assert.Equal(DiagnosticSeverity.Error, reported.Severity);
        Assert.Contains("Acme.Inbox.InboxRowMoved", reported.GetMessage());
        Assert.Contains("ConversationId", reported.GetMessage());
    }

    static ImmutableArray<Diagnostic> Diagnose(IIncrementalGenerator generator, string holder)
    {
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(Compile(holder), out _, out var diagnostics);

        return diagnostics;
    }

    static string Generate(IIncrementalGenerator generator, string holder)
    {
        var compilation = Compile(holder);

        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
        Assert.Empty(output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));

        return Assert.Single(driver.GetRunResult().GeneratedTrees).ToString();
    }

    static Compilation Compile(string holder)
    {
        return new CompilationUnitBuilder()
            .AddSource("Messages.cs", Messages + holder)
            .AddFrameworkReferences("net10.0")
            .AddContainingAssembly<Microsoft.Extensions.DependencyInjection.IServiceCollection>()
            .AddContainingAssembly<GeneratedAttribute>()
            .AddContainingAssembly<IMessage>()
            .AddContainingAssembly<PushedAttribute>()
            .AddContainingAssembly<Mediator>()
            .AddContainingAssembly<PushPublisher<IMessage>>()
            .AddContainingAssembly<Microsoft.AspNetCore.SignalR.Hub>()
            .AddContainingAssembly<Microsoft.AspNetCore.SignalR.IHubContext<PushHub>>()
            .Build("SignalRTestAssembly");
    }
}
