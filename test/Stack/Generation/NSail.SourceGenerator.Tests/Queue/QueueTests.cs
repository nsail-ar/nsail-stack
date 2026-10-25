// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NSail.Messaging;
using NSail.Messaging.Annotations;
using NSail.Messaging.Runtime;
using NSail.SourceGeneration.Annotations;
using NSail.SourceGeneration.Testing;

namespace NSail.SourceGenerator.Tests.Queue;

/// <summary>nsail#1930: the queue's two ends are generated from one walk, so what one host
/// stored the other can name. Each test compiles what it generates against the real Stack
/// types — a registration naming a missing sender or consumer would be an error here, not a
/// send that meets SenderNotFound in production.</summary>
public class QueueTests
{
    const string Messages = """
        using Microsoft.Extensions.DependencyInjection;
        using NSail.Messaging;
        using NSail.Messaging.Annotations;
        using NSail.SourceGeneration.Annotations;

        namespace Acme.Billing;

        [Queue]
        public class PostInvoice : IMessage
        {
            public System.Guid InvoiceId { get; set; }
        }

        public class CancelInvoice : IMessage
        {
        }
        """;

    [Fact]
    public void TheSendingHostRegistersASenderForEveryQueuedMessageAndNothingElse()
    {
        var generated = Generate(new Generation.Queue.QueueProducersSourceGenerator(), """
            public static partial class Producers
            {
                [Generated(global::NSail.SourceGeneration.Annotations.Queue.Producers)]
                public static partial void AddBillingQueueProducers(this IServiceCollection services);
            }
            """);

        Assert.Contains(
            "services.AddScoped<global::NSail.Messaging.Runtime.Sending.ISender<global::Acme.Billing.PostInvoice>, global::NSail.Messaging.Queue.QueueSender<global::Acme.Billing.PostInvoice>>();",
            generated);

        // Unmarked, it keeps whatever sender its own declaration asks for.
        Assert.DoesNotContain("CancelInvoice", generated);
    }

    [Fact]
    public void TheDrainingHostRegistersAConsumerForTheSameSet()
    {
        var generated = Generate(new Generation.Queue.QueueConsumersSourceGenerator(), """
            public static partial class Consumers
            {
                [Generated(global::NSail.SourceGeneration.Annotations.Queue.Consumers)]
                public static partial void AddBillingQueueConsumers(this IServiceCollection services);
            }
            """);

        Assert.Contains(
            "services.AddSingleton<global::NSail.Messaging.Queue.QueueConsumer>(new global::NSail.Messaging.Queue.QueueConsumer<global::Acme.Billing.PostInvoice>());",
            generated);

        Assert.DoesNotContain("CancelInvoice", generated);
    }

    /// <summary>A queued send answers "accepted" at the commit and the handler runs long
    /// after, so there is no outcome to hand back.</summary>
    [Fact]
    public void QueueOnAMessageThatReturnsAResultIsABuildError()
    {
        var diagnostics = Diagnose(new Generation.Queue.QueueProducersSourceGenerator(), """
            [Queue]
            public class CountInvoices : IMessage<int>
            {
            }

            public static partial class Producers
            {
                [Generated(global::NSail.SourceGeneration.Annotations.Queue.Producers)]
                public static partial void AddBillingQueueProducers(this IServiceCollection services);
            }
            """);

        var reported = Assert.Single(diagnostics.Where(d => d.Id == "NSG014"));

        Assert.Equal(DiagnosticSeverity.Error, reported.Severity);
        Assert.Contains("Acme.Billing.CountInvoices", reported.GetMessage());
    }

    /// <summary>Both attributes register an ISender, which the pipeline can only refuse at
    /// runtime as MultipleSenders. The declaration is where it is visible.</summary>
    [Fact]
    public void QueueBesideHttpIsABuildError()
    {
        var diagnostics = Diagnose(new Generation.Queue.QueueConsumersSourceGenerator(), """
            [Queue]
            [Http(Method.Post, "/billing/invoices/void")]
            public class VoidInvoice : IMessage
            {
            }

            public static partial class Consumers
            {
                [Generated(global::NSail.SourceGeneration.Annotations.Queue.Consumers)]
                public static partial void AddBillingQueueConsumers(this IServiceCollection services);
            }
            """);

        var reported = Assert.Single(diagnostics.Where(d => d.Id == "NSG015"));

        Assert.Equal(DiagnosticSeverity.Error, reported.Severity);
        Assert.Contains("Acme.Billing.VoidInvoice", reported.GetMessage());
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
            .AddContainingAssembly<QueueAttribute>()
            .AddContainingAssembly<Mediator>()
            .AddContainingAssembly<global::NSail.Messaging.Queue.QueueSender<IMessage>>()
            .AddContainingAssembly<Microsoft.EntityFrameworkCore.DbContext>()
            .Build("QueueTestAssembly");
    }
}
