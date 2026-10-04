// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

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
