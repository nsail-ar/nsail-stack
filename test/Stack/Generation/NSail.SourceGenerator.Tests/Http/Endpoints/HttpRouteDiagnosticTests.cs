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
using System.Collections.Immutable;
using System.Linq;

namespace NSail.SourceGenerator.Tests.Http.Endpoints;

public class HttpRouteDiagnosticTests
{
    const string UnboundTokenSource = """
        using Microsoft.Extensions.DependencyInjection;
        using Microsoft.AspNetCore.Routing;
        using NSail.Messaging;
        using NSail.Messaging.Annotations;
        using NSail.SourceGeneration.Annotations;

        namespace Acme.Clinic.Prescriptions;

        [Http(Method.Get, "api/prescriptions/{patientId}/items/{itemId}")]
        public class ListPrescriptionItems : IMessage
        {
            [AsRoute]
            public System.Guid PatientId { get; set; }
        }

        public static partial class Endpoints
        {
            [Generated(NSail.SourceGeneration.Annotations.Http.Endpoints)]
            public static partial void AddClinicEndpoints(this IServiceCollection services);
        }
        """;

    [Fact]
    public void An_unbound_route_token_is_a_build_error()
    {
        var diagnostics = Run(UnboundTokenSource);

        var reported = Assert.Single(diagnostics.Where(d => d.Id == "NSG001"));

        Assert.Equal(DiagnosticSeverity.Error, reported.Severity);

        var text = reported.GetMessage();

        Assert.Contains("api/prescriptions/{patientId}/items/{itemId}", text);
        Assert.Contains("Acme.Clinic.Prescriptions.ListPrescriptionItems", text);
        Assert.Contains("{itemId}", text);

        // The whole point of the diagnostic: the segment is dropped from the mapped route, so
        // without an error the endpoint answers at a URL the message never declared.
        Assert.NotEmpty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
    }

    [Fact]
    public void A_throttled_identity_naming_no_property_is_a_build_error()
    {
        var diagnostics = Run(ThrottledSource("Login", "Missing"));

        var reported = Assert.Single(diagnostics.Where(d => d.Id == "NSG016"));

        Assert.Equal(DiagnosticSeverity.Error, reported.Severity);
        Assert.Contains("Missing", reported.GetMessage());
        Assert.Empty(Run(ThrottledSource("Login", "Login")).Where(d => d.Id == "NSG016"));
    }

    [Fact]
    public void A_throttled_identity_that_is_not_bound_from_the_body_is_a_build_error()
    {
        var source = ThrottledSource("Login", "Login").Replace("Method.Post", "Method.Get");

        Assert.Single(Run(source).Where(d => d.Id == "NSG016"));
    }

    [Fact]
    public void A_throttled_identity_that_is_renamed_or_not_a_string_is_a_build_error()
    {
        var renamed = ThrottledSource("Login", "Login")
            .Replace("public string Login", "[JsonPropertyName(\"u\")] public string Login")
            + "\npublic sealed class JsonPropertyNameAttribute(string name) : System.Attribute;\n";
        var number = ThrottledSource("Login", "Login").Replace("public string Login { get; set; } = \"\";", "public int Login { get; set; }");

        Assert.Single(Run(renamed).Where(d => d.Id == "NSG016"));
        Assert.Single(Run(number).Where(d => d.Id == "NSG016"));
    }

    static string ThrottledSource(string property, string identity)
    {
        return $$"""
            using Microsoft.Extensions.DependencyInjection;
            using Microsoft.AspNetCore.Routing;
            using NSail.Messaging;
            using NSail.Messaging.Annotations;
            using NSail.SourceGeneration.Annotations;

            namespace Acme.Clinic.Access;

            [Http(Method.Post, "api/sign-in")]
            [Throttled(20, Identity = "{{identity}}")]
            public class SignIn : IMessage
            {
                public string {{property}} { get; set; } = "";
            }

            public static partial class Endpoints
            {
                [Generated(NSail.SourceGeneration.Annotations.Http.Endpoints)]
                public static partial void AddClinicEndpoints(this IServiceCollection services);
            }
            """;
    }

    const string BoundTokenSource = """
        using Microsoft.Extensions.DependencyInjection;
        using Microsoft.AspNetCore.Routing;
        using NSail.Messaging;
        using NSail.Messaging.Annotations;
        using NSail.SourceGeneration.Annotations;

        namespace Acme.Clinic.Prescriptions;

        [Http(Method.Get, "api/prescriptions/{patientId}/items/{itemId}")]
        public class ListPrescriptionItems : IMessage
        {
            [AsRoute]
            public System.Guid PatientId { get; set; }

            [AsRoute]
            public System.Guid ItemId { get; set; }
        }

        public static partial class Endpoints
        {
            [Generated(NSail.SourceGeneration.Annotations.Http.Endpoints)]
            public static partial void AddClinicEndpoints(this IServiceCollection services);
        }
        """;

    [Fact]
    public void A_token_bound_by_a_route_property_reports_nothing()
    {
        var diagnostics = Run(BoundTokenSource);

        Assert.Empty(diagnostics.Where(d => d.Id == "NSG001"));
        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
    }

    [Theory]
    [InlineData("api/items/{id:guid}")]
    [InlineData("api/items/{id?}")]
    [InlineData("api/items/{id:int?}")]
    [InlineData("api/items/{*id}")]
    [InlineData("api/items/{**id}")]
    [InlineData("api/items/{id=5}")]
    public void A_token_with_a_constraint_optional_marker_or_catch_all_binds_by_name(string route)
    {
        var diagnostics = Run(RouteSource($"[Http(Method.Get, \"{route}\")]", "[AsRoute] public System.Guid Id { get; set; }"));

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
    }

    [Fact]
    public void A_mixed_segment_is_a_build_error_naming_the_template()
    {
        var diagnostics = Run(RouteSource("[Http(Method.Get, \"api/v{version}/items\")]", "public int Version { get; set; }"));

        var reported = Assert.Single(diagnostics.Where(d => d.Id == "NSG017"));

        Assert.Contains("api/v{version}/items", reported.GetMessage());
    }

    [Fact]
    public void A_route_member_with_no_token_is_a_build_error()
    {
        var diagnostics = Run(RouteSource("[Http(Method.Get, \"api/items\")]", "[AsRoute] public System.Guid Id { get; set; }"));

        Assert.Single(diagnostics.Where(d => d.Id == "NSG018"));
    }

    [Fact]
    public void Two_messages_on_one_verb_and_normalised_path_are_a_build_error()
    {
        var same = Run(TwoRoutes("Method.Get", "api/x/{a}", "Method.Get", "api/x/{b:guid}"));
        var otherVerb = Run(TwoRoutes("Method.Get", "api/x/{a}", "Method.Post", "api/x/{a}"));

        Assert.Single(same.Where(d => d.Id == "NSG019"));
        Assert.Empty(otherVerb.Where(d => d.Id == "NSG019"));
    }

    static string RouteSource(string http, string member)
    {
        return $$"""
            using Microsoft.Extensions.DependencyInjection;
            using Microsoft.AspNetCore.Routing;
            using NSail.Messaging;
            using NSail.Messaging.Annotations;
            using NSail.SourceGeneration.Annotations;

            namespace Acme.Clinic.Items;

            {{http}}
            public class GetItem : IMessage
            {
                {{member}}
            }

            public static partial class Endpoints
            {
                [Generated(NSail.SourceGeneration.Annotations.Http.Endpoints)]
                public static partial void AddClinicEndpoints(this IServiceCollection services);
            }
            """;
    }

    static string TwoRoutes(string verbA, string pathA, string verbB, string pathB)
    {
        return $$"""
            using Microsoft.Extensions.DependencyInjection;
            using Microsoft.AspNetCore.Routing;
            using NSail.Messaging;
            using NSail.Messaging.Annotations;
            using NSail.SourceGeneration.Annotations;

            namespace Acme.Clinic.Items;

            [Http({{verbA}}, "{{pathA}}")]
            public class MessageA : IMessage
            {
                [AsRoute] public string A { get; set; } = "";
            }

            [Http({{verbB}}, "{{pathB}}")]
            public class MessageB : IMessage
            {
                [AsRoute] public string B { get; set; } = "";
            }

            public static partial class Endpoints
            {
                [Generated(NSail.SourceGeneration.Annotations.Http.Endpoints)]
                public static partial void AddClinicEndpoints(this IServiceCollection services);
            }
            """;
    }

    static ImmutableArray<Diagnostic> Run(string source)
    {
        var builder = new CompilationUnitBuilder()
            .AddSource("Messages.cs", source)
            .AddFrameworkReferences("net10.0")
            .AddContainingAssembly<GeneratedAttribute>()
            .AddContainingAssembly<IMessage>()
            .AddContainingAssembly<HttpAttribute>()
            .AddContainingAssembly<IEndpointRouteBuilder>();

        var compilation = builder.Build("HttpRouteDiagnosticTestAssembly");

        GeneratorDriver driver = CSharpGeneratorDriver.Create(new HttpEndpointsSourceGenerator());

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

        return diagnostics;
    }
}
