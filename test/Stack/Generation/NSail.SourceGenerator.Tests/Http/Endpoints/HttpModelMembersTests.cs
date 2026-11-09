// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.AspNetCore.Routing;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NSail.Messaging;
using NSail.Messaging.Annotations;
using NSail.SourceGeneration.Annotations;
using NSail.SourceGeneration.Testing;
using NSail.SourceGenerator.Generation.Http;
using System;
using System.Globalization;
using System.Linq;

namespace NSail.SourceGenerator.Tests.Http.Endpoints;

public class HttpModelMembersTests
{
    const string Header = """
        using Microsoft.Extensions.DependencyInjection;
        using Microsoft.AspNetCore.Routing;
        using NSail.Messaging;
        using NSail.Messaging.Annotations;
        using NSail.SourceGeneration.Annotations;

        namespace Acme.Clinic.Members;

        public static partial class Endpoints
        {
            [Generated(NSail.SourceGeneration.Annotations.Http.Endpoints)]
            public static partial void AddClinicEndpoints(this IServiceCollection services);
        }

        public abstract record BaseMessage
        {
            public virtual string Title { get; set; } = "";
        }

        """;

    const string Members = Header + """
        [Http(Method.Post, "api/things")]
        public record SaveThing : BaseMessage, IMessage
        {
            public string Name { get; set; } = "";
            public string Computed => Name + "!";
            public static string Shared { get; set; } = "";
            public string ReadOnly { get; } = "";
            public string this[int i] { get => ""; set { } }
            [System.Text.Json.Serialization.JsonIgnore]
            public string Skipped { get; set; } = "";
            public override string Title { get; set; } = "";
        }
        """;

    const string Hidden = Header + """
        public record HiddenBase { public virtual string Secret { get; set; } = ""; }

        public class NarrowBase { public string Name { get; set; } = ""; }

        [Http(Method.Post, "api/hidden")]
        public record SaveHidden : HiddenBase, IMessage
        {
            [System.Text.Json.Serialization.JsonIgnore]
            public override string Secret { get; set; } = "";
        }

        [Http(Method.Post, "api/narrow")]
        public class SaveNarrow : NarrowBase, IMessage
        {
            public new string Name { get; } = "";
        }
        """;

    const string Names = Header + """
        [Http(Method.Get, "api/things/{id}")]
        public class FindThing : IMessage
        {
            public System.Guid Id { get; set; }
            [AsQuery] public string Message { get; set; } = "";
            [AsQuery] public string Builder { get; set; } = "";
            [AsQuery] public string Result { get; set; } = "";
            [AsQuery] public string Event { get; set; } = "";
            [AsQuery] public int Params { get; set; }
            [AsQuery] public string? Search { get; set; }
        }
        """;

    const string Defaults = Header + """
        [Http(Method.Get, "api/things")]
        public class ListThings : IMessage
        {
            [AsQuery] public string Search { get; set; } = "";
            [AsQuery] public required string Needed { get; set; }
        }
        """;

    [Fact]
    public void Only_settable_public_instance_members_enter_the_model_once()
    {
        var generated = Run(Members, out var errors);

        Assert.Empty(errors);

        var dto = generated.Single(t => t.Contains("class SaveThingBody", StringComparison.Ordinal));

        Assert.Contains("Name", dto);
        Assert.Contains("Title", dto);
        Assert.Equal(1, Count(dto, " Title "));
        Assert.DoesNotContain("Computed", dto);
        Assert.DoesNotContain("Shared", dto);
        Assert.DoesNotContain("ReadOnly", dto);
        Assert.DoesNotContain("Skipped", dto);
        Assert.DoesNotContain("EqualityContract", dto);
    }

    [Fact]
    public void An_ignored_or_narrowed_override_hides_the_inherited_member_too()
    {
        var generated = Run(Hidden, out var errors);

        Assert.Empty(errors);

        foreach (var dto in generated.Where(t => t.Contains("class SaveHiddenBody", StringComparison.Ordinal) || t.Contains("class SaveNarrowBody", StringComparison.Ordinal)))
        {
            Assert.DoesNotContain("Secret", dto);
            Assert.DoesNotContain("Name", dto);
        }
    }

    [Fact]
    public void Parameter_names_never_collide_with_keywords_locals_or_the_culture()
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("tr-TR");

        try
        {
            var generated = Run(Names, out var errors);

            Assert.Empty(errors);

            var entry = generated.Single(t => t.Contains("class FindThingEndpointEntry", StringComparison.Ordinal));

            Assert.Contains("[FromRoute] System.Guid id,", entry);
            Assert.Contains("[FromQuery(Name = \"message\")]", entry);
            Assert.Contains("string? nsailMessage,", entry);
            Assert.Contains("[FromQuery(Name = \"builder\")]", entry);
            Assert.Contains("string? @event,", entry);
            Assert.Contains("@params", entry);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void A_non_required_non_nullable_reference_query_member_binds_optional()
    {
        var generated = Run(Defaults, out var errors);

        Assert.Empty(errors);

        var entry = generated.Single(t => t.Contains("class ListThingsEndpointEntry", StringComparison.Ordinal));

        Assert.Contains("string? search,", entry);
        Assert.Contains("if (search is not null)", entry);
        Assert.Contains("string needed,", entry);
    }

    static int Count(string text, string fragment)
    {
        return text.Split(fragment).Length - 1;
    }

    static string[] Run(string source, out Diagnostic[] errors)
    {
        var compilation = new CompilationUnitBuilder()
            .AddSource("Messages.cs", source)
            .AddFrameworkReferences("net10.0")
            .AddContainingAssembly<GeneratedAttribute>()
            .AddContainingAssembly<IMessage>()
            .AddContainingAssembly<HttpAttribute>()
            .AddContainingAssembly<IEndpointRouteBuilder>()
            .AddContainingAssembly<Microsoft.AspNetCore.Mvc.FromQueryAttribute>()
            .AddContainingAssembly<Microsoft.AspNetCore.Routing.RouteData>()
            .AddContainingAssembly<Microsoft.AspNetCore.Http.HttpContext>()
            .AddReference(MetadataReference.CreateFromFile(typeof(Microsoft.AspNetCore.Http.TypedResults).Assembly.Location))
            .AddContainingAssembly<NSail.Messaging.WebApi.IEndpointEntry>()
            .AddContainingAssembly<NSail.Metadata.MetadataProvider>()
            .AddContainingAssembly<NSail.Messaging.Runtime.Pipelines.InProcessSendPipeline<IMessage>>()
            .Build("HttpModelMembersTestAssembly");

        GeneratorDriver driver = CSharpGeneratorDriver.Create(new HttpEndpointsSourceGenerator());

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var updated, out var diagnostics);

        errors = [.. diagnostics.Concat(updated.GetDiagnostics()).Where(d => d.Severity == DiagnosticSeverity.Error)];

        return [.. driver.GetRunResult().GeneratedTrees.Select(t => t.ToString())];
    }
}
