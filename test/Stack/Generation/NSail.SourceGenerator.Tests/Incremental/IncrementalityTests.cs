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

    // The compiler's own inputs — the compilation, and the attribute lookup that takes it — are
    // modified by any edit.
    static readonly HashSet<string> CompilerInputs = ["Compilation", "CompilationOptions"];

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

    // A per-file step whose type reads a fact from another file must not keep its old answer: an
    // edit to the base in A.cs changes what the generator says about the type in B.cs.
    const string Base = """
        namespace Acme.Inbox;

        public class NoteBase
        {
            public string Subject { get; set; } = "";
        }

        public class MovedBase
        {
            // moved
        }
        """;

    const string Derived = """
        using NSail.Mapping.Annotations;
        using NSail.Messaging;
        using NSail.Messaging.Annotations;

        namespace Acme.Inbox;

        public class PostNote : IMessage
        {
            public string Subject { get; set; } = "";

            public string Body { get; set; } = "";
        }

        [Entity, MapFrom(typeof(PostNote))]
        public class Note : NoteBase
        {
            public System.Guid Id { get; set; }
        }

        [Pushed]
        public class NoteMoved : MovedBase, IMessage
        {
        }
        """;

    [Fact]
    public void An_edit_to_a_base_in_another_file_regenerates_the_derived_mapper()
    {
        var driver = Driver(new MappersSourceGenerator());
        var compilation = CompileDerived("Mappers.Entities");

        driver = driver.RunGenerators(compilation);

        var before = driver.GetRunResult();

        driver = driver.RunGenerators(Edit(compilation, "Base.cs", Base.Replace("public string Subject", "public string Body { get; set; } = \"\"; public string Subject")));

        var after = driver.GetRunResult();

        Assert.Contains(before.Diagnostics, d => d.Id == "NSG011" && d.GetMessage().Contains("PostNote.Body"));
        Assert.DoesNotContain(after.Diagnostics, d => d.Id == "NSG011");
        Assert.DoesNotContain(".Body", Text(before));
        Assert.Contains(".Body", Text(after));
        Assert.Contains(after.Results[0].TrackedOutputSteps.SelectMany(p => p.Value).SelectMany(r => r.Outputs), o => o.Reason == IncrementalStepRunReason.Modified);
    }

    [Fact]
    public void An_edit_to_a_base_in_another_file_reaches_the_derived_pushed_event()
    {
        var driver = Driver(new PushPublishersSourceGenerator());
        var compilation = CompileDerived("Push.Publishers");

        driver = driver.RunGenerators(compilation);

        var before = driver.GetRunResult();

        driver = driver.RunGenerators(Edit(compilation, "Base.cs", Base.Replace("// moved", "public string Leak { get; set; } = \"\";")));

        var after = driver.GetRunResult();

        Assert.DoesNotContain(before.Diagnostics, d => d.Id == "NSG003");
        Assert.Contains(after.Diagnostics, d => d.Id == "NSG003" && d.GetMessage().Contains("NoteMoved"));
    }

    // A finding is located in its file's tree, and an unedited file keeps its tree.
    [Theory]
    [InlineData(nameof(MappersSourceGenerator), "Mappers.Entities", "NSG011")]
    [InlineData(nameof(PushPublishersSourceGenerator), "Push.Publishers", "NSG003")]
    public void A_finding_survives_an_unrelated_edit_cached(string generatorName, string target, string id)
    {
        var driver = Driver(Create(generatorName));
        var compilation = CompileDerived(target);

        compilation = Edit(compilation, "Base.cs", Base.Replace("// moved", "public string Leak { get; set; } = \"\";"));
        driver = driver.RunGenerators(compilation);
        Assert.Contains(driver.GetRunResult().Diagnostics, d => d.Id == id);

        driver = driver.RunGenerators(Edit(compilation, "Unrelated.cs", Unrelated + "\n// a comment\n"));

        Assert.Contains(driver.GetRunResult().Diagnostics, d => d.Id == id);
        AssertNothingRan(driver);
    }

    static string Text(GeneratorDriverRunResult result)
    {
        return string.Concat(result.GeneratedTrees.Select(t => t.ToString()));
    }

    static Compilation CompileDerived(string target)
    {
        var compilation = Compile(Holder(target, scoped: false), Unrelated);
        var domain = compilation.SyntaxTrees.Single(t => t.FilePath.EndsWith("Domain.cs", StringComparison.Ordinal));

        return compilation
            .RemoveSyntaxTrees(domain)
            .AddSyntaxTrees(
                CSharpSyntaxTree.ParseText(Base, (CSharpParseOptions)domain.Options, "Base.cs"),
                CSharpSyntaxTree.ParseText(Derived, (CSharpParseOptions)domain.Options, "Derived.cs"));
    }

    static void AssertNothingRan(GeneratorDriver driver)
    {
        var result = Assert.Single(driver.GetRunResult().Results);

        // Every step but the compiler's, so a step named anew is covered without being listed.
        var steps = result.TrackedSteps
            .Where(p => !CompilerInputs.Contains(p.Key) && !p.Key.Contains("_ForAttribute"))
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
            .AddFrameworkReferences()
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

        return compilation.ReplaceSyntaxTree(old, CSharpSyntaxTree.ParseText(text, (CSharpParseOptions)old.Options, old.FilePath));
    }
}

static class DriverExtensions
{
    public static GeneratorDriver RunGenerators(this GeneratorDriver driver, Compilation compilation)
    {
        return driver.RunGenerators(compilation, default);
    }
}
