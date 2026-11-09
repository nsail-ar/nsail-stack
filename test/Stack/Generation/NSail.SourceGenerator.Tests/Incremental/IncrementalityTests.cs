// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NSail.Mapping;
using NSail.Mapping.Annotations;
using NSail.Messaging;
using NSail.Messaging.Annotations;
using NSail.Messaging.Runtime;
using NSail.Messaging.WebApi.Push;
using NSail.SourceGeneration.Annotations;
using NSail.SourceGeneration.Testing;
using NSail.SourceGenerator.Generation.Http;
using NSail.SourceGenerator.Generation.Mappers;
using NSail.SourceGenerator.Generation.Policies;
using NSail.SourceGenerator.Generation.Push;
using NSail.SourceGenerator.Generation.Queue;
using NSail.SourceGenerator.Generation.Services;

namespace NSail.SourceGenerator.Tests.Incremental;

/// <summary>A generator that re-reads the project on every keystroke makes the IDE crawl, and
/// nothing else notices: the output is the same. Each generator runs twice over a project with
/// a trivial edit to a file it does not read, and every step it ran must come back cached or
/// unchanged — a model that holds a symbol, a diagnostic or a plain list is never equal to
/// itself, so it fails here.</summary>
public class IncrementalityTests
{
    const string Domain = """
        using Microsoft.Extensions.DependencyInjection;
        using Microsoft.EntityFrameworkCore;
        using NSail.Injection.Annotations;
        using NSail.Mapping.Annotations;
        using NSail.Messaging;
        using NSail.Messaging.Annotations;
        using NSail.Security.Annotations;
        using NSail.SourceGeneration.Annotations;

        namespace Acme.Inbox;

        [Http(Method.Get, "/inbox/{id}")]
        public class GetInbox : IMessage<string>
        {
            public System.Guid Id { get; set; }

            public string? Filter { get; set; }
        }

        [Http(Method.Post, "/inbox")]
        public class PostInbox : IMessage
        {
            public string Subject { get; set; } = "";
        }

        [Pushed]
        public class InboxMoved : IMessage
        {
        }

        [Queue]
        public class DeliverInbox : IMessage
        {
        }

        [Injectable]
        public class InboxService
        {
        }

        public class InboxRow
        {
            public System.Guid Id { get; set; }

            public string Subject { get; set; } = "";
        }

        [Entity, MapFrom(typeof(PostInbox))]
        public class InboxEntry
        {
            public System.Guid Id { get; set; }

            public string Subject { get; set; } = "";
        }

        public class InboxContext : DbContext
        {
            public DbSet<InboxEntry> Entries { get; set; } = null!;
        }
        """;

    const string Unrelated = """
        namespace Acme.Unrelated;

        public class Calendar
        {
            public int Today() { return 1; }
        }
        """;

    static readonly HashSet<string> Named = ["Targets", "MetadataTemplate", "LocalTypes", "SourceTypes", "Scoped", "Model", "Mappers", "Modelled"];

    public static TheoryData<string, string> Generators => new()
    {
        { nameof(HttpEndpointsSourceGenerator), "Http.Endpoints" },
        { nameof(HttpClientsSourceGenerator), "Http.Clients" },
        { nameof(HttpInProcessSourceGenerator), "Http.InProcess" },
        { nameof(PushPublishersSourceGenerator), "Push.Publishers" },
        { nameof(PushClientsSourceGenerator), "Push.Clients" },
        { nameof(QueueProducersSourceGenerator), "Queue.Producers" },
        { nameof(QueueConsumersSourceGenerator), "Queue.Consumers" },
        { nameof(PolicyHandlersSourceGenerator), "Policies.Handlers" },
        { nameof(ServiceRegistrationSourceGenerator), "Services.Registration" },
        { nameof(MappersSourceGenerator), "Mappers.Entities" },
    };

    [Theory]
    [MemberData(nameof(Generators))]
    public void An_unrelated_edit_leaves_every_step_cached_or_unchanged(string generatorName, string target)
    {
        var generator = Create(generatorName);
        var driver = Driver(generator);
        var compilation = Compile(Holder(target, scoped: false), Unrelated);

        driver = driver.RunGenerators(compilation);
        Assert.NotEmpty(driver.GetRunResult().GeneratedTrees);

        driver = driver.RunGenerators(Edit(compilation, "Unrelated.cs", Unrelated + "\n// a comment\n"));

        AssertNothingRan(driver);
    }

    [Theory]
    [MemberData(nameof(Generators))]
    public void A_holder_scoped_with_Source_is_as_stable(string generatorName, string target)
    {
        var generator = Create(generatorName);
        var driver = Driver(generator);
        var compilation = Compile(Holder(target, scoped: true), Unrelated);

        driver = driver.RunGenerators(compilation);
        Assert.NotEmpty(driver.GetRunResult().GeneratedTrees);

        driver = driver.RunGenerators(Edit(compilation, "Unrelated.cs", Unrelated + "\n// a comment\n"));

        AssertNothingRan(driver);
    }

    [Fact]
    public void An_edit_that_changes_what_is_read_still_regenerates()
    {
        var driver = Driver(new PushPublishersSourceGenerator());
        var compilation = Compile(Holder("Push.Publishers", scoped: false), Unrelated);

        driver = driver.RunGenerators(compilation);

        var before = Assert.Single(driver.GetRunResult().GeneratedTrees).ToString();

        var edited = Edit(compilation, "Unrelated.cs", Unrelated + "\n[NSail.Messaging.Annotations.Pushed] public class Late : NSail.Messaging.IMessage { }\n");
        driver = driver.RunGenerators(edited);

        var after = Assert.Single(driver.GetRunResult().GeneratedTrees).ToString();

        Assert.DoesNotContain("Late", before);
        Assert.Contains("Late", after);
    }

    static void AssertNothingRan(GeneratorDriver driver)
    {
        var result = Assert.Single(driver.GetRunResult().Results);

        // The steps the generator names, and what it registers. The compiler's own inputs — the
        // compilation, and the attribute lookup that takes it — are modified by any edit.
        var steps = result.TrackedSteps
            .Where(p => Named.Contains(p.Key))
            .Concat(result.TrackedOutputSteps)
            .ToArray();

        Assert.Contains(steps, p => p.Key == "Targets");
        Assert.NotEmpty(result.TrackedOutputSteps);

        foreach (var (name, runs) in steps)
        {
            foreach (var output in runs.SelectMany(r => r.Outputs))
            {
                Assert.True(
                    output.Reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged,
                    $"{name}: {output.Reason}");
            }
        }
    }

    static IIncrementalGenerator Create(string name)
    {
        var type = typeof(MappersSourceGenerator).Assembly.GetTypes().Single(t => t.Name == name);

        return (IIncrementalGenerator)Activator.CreateInstance(type)!;
    }

    static GeneratorDriver Driver(IIncrementalGenerator generator)
    {
        return CSharpGeneratorDriver.Create(
            [generator.AsSourceGenerator()],
            driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: true));
    }

    static string Holder(string target, bool scoped)
    {
        var source = scoped ? "[Source(Pattern = \"Acme.Inbox.*\")]" : string.Empty;
        var area = target.Split('.')[0];
        var kind = target.Split('.')[1];

        return $$"""
            namespace Acme.Holders;

            using Microsoft.Extensions.DependencyInjection;
            using NSail.SourceGeneration.Annotations;

            public static partial class Holder
            {
                {{source}}
                [Generated(global::NSail.SourceGeneration.Annotations.{{area}}.{{kind}})]
                public static partial void Add{{area}}{{kind}}(this IServiceCollection services);
            }
            """;
    }

    static Compilation Compile(string holder, string unrelated)
    {
        return new CompilationUnitBuilder()
            .AddSource("Domain.cs", Domain)
            .AddSource("Holder.cs", holder)
            .AddSource("Unrelated.cs", unrelated)
            .AddFrameworkReferences("net10.0")
            .AddContainingAssembly<Microsoft.Extensions.DependencyInjection.IServiceCollection>()
            .AddContainingAssembly<GeneratedAttribute>()
            .AddContainingAssembly<IMessage>()
            .AddContainingAssembly<PushedAttribute>()
            .AddContainingAssembly<QueueAttribute>()
            .AddContainingAssembly<HttpAttribute>()
            .AddContainingAssembly<Mediator>()
            .AddContainingAssembly<PushPublisher<IMessage>>()
            .AddContainingAssembly<global::NSail.Messaging.Queue.QueueSender<IMessage>>()
            .AddContainingAssembly<MapFromAttribute>()
            .AddContainingAssembly<MapContext>()
            .AddContainingAssembly<NSail.Security.Annotations.PolicyFieldAttribute>()
            .AddContainingAssembly<NSail.Injection.Annotations.InjectableAttribute>()
            .AddContainingAssembly<Microsoft.EntityFrameworkCore.DbContext>()
            .Build("IncrementalAssembly");
    }

    static Compilation Edit(Compilation compilation, string fileName, string text)
    {
        var old = compilation.SyntaxTrees.Single(t => t.FilePath.EndsWith(fileName, StringComparison.Ordinal));

        return compilation.ReplaceSyntaxTree(old, CSharpSyntaxTree.ParseText(text, path: old.FilePath));
    }
}

static class DriverExtensions
{
    public static GeneratorDriver RunGenerators(this GeneratorDriver driver, Compilation compilation)
    {
        return driver.RunGenerators(compilation, default);
    }
}
