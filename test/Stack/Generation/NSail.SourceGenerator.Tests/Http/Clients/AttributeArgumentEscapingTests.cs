// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.DependencyInjection;
using NSail.Messaging;
using NSail.Messaging.Annotations;
using NSail.Messaging.Http;
using NSail.Messaging.Runtime.Sending;
using NSail.SourceGeneration.Annotations;
using NSail.SourceGeneration.Testing;
using NSail.SourceGenerator.Generation.Http;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net.Http;

namespace NSail.SourceGenerator.Tests.Http.Clients;

public class AttributeArgumentEscapingTests
{
    [Theory]
    [InlineData("""@"^\d{14}$" """, "^\\d{14}$")]
    [InlineData("\"has a \\\"quote\\\" inside\"", "has a \"quote\" inside")]
    [InlineData("""
        @"line one
        line two"
        """, "line one\nline two")]
    public void A_string_attribute_argument_reaches_the_generated_body_escaped_and_parses(
        string literalSource,
        string expectedPattern)
    {
        var builder = new CompilationUnitBuilder()
            .AddSource("Messages.cs", $$"""
                global using System.Net.Http;

                using Microsoft.Extensions.DependencyInjection;
                using NSail.Messaging;
                using NSail.Messaging.Annotations;
                using NSail.SourceGeneration.Annotations;
                using System.ComponentModel.DataAnnotations;

                namespace Acme.Clinic.Prescriptions;

                [Http(Method.Post, "api/prescriptions")]
                public class CreatePrescription : IMessage
                {
                    [RegularExpression({{literalSource}})]
                    public string DocumentNumber { get; set; } = string.Empty;
                }

                public static partial class Clients
                {
                    [Generated(Http.Endpoints)]
                    public static partial void AddClinicEndpoints(this IServiceCollection services);
                }
                """)
            .AddFrameworkReferences("net10.0")
            .AddContainingAssembly<GeneratedAttribute>()
            .AddContainingAssembly<IMessage>()
            .AddContainingAssembly<HttpAttribute>()
            .AddContainingAssembly<RegularExpressionAttribute>()
            .AddContainingAssembly<IServiceCollection>()
            .AddContainingAssembly<IHttpClientFactory>()
            .AddContainingAssembly<ISender<IMessage>>()
            .AddContainingAssembly<HttpSender<IMessage>>()
            .AddContainingAssembly<System.Text.Json.JsonSerializerOptions>()
            .AddContainingAssembly<Microsoft.AspNetCore.Routing.IEndpointRouteBuilder>();

        var compilation = builder.Build("AttributeArgumentEscapingTestAssembly");

        var generator = new HttpEndpointsSourceGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        // A raw pattern like @"^\d{14}$" appears identically in the generated text whether the
        // argument is escaped correctly or not, so grepping the text cannot tell the two apart —
        // only parsing the generated tree exposes a malformed literal (CS1009 and friends).
        var errors = driver.GetRunResult().GeneratedTrees
            .SelectMany(tree => tree.GetDiagnostics())
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();
        Assert.True(errors.Count == 0, string.Join("\n", errors));

        var generated = string.Join("\n", driver.GetRunResult().GeneratedTrees.Select(tree => tree.ToString()));

        Assert.Contains("RegularExpressionAttribute(", generated);
        Assert.Contains(SymbolDisplay.FormatLiteral(expectedPattern, quote: true), generated);
    }

    [Theory]
    [InlineData("es-AR")]
    [InlineData("tr-TR")]
    public void Named_type_array_and_double_arguments_reach_the_generated_body_and_the_client_compiles(string culture)
    {
        var previous = System.Globalization.CultureInfo.CurrentCulture;
        System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo(culture);

        try
        {
            var builder = new CompilationUnitBuilder()
                .AddSource("Messages.cs", """
                    global using System.Net.Http;

                    using Microsoft.Extensions.DependencyInjection;
                    using NSail.Messaging;
                    using NSail.Messaging.Annotations;
                    using NSail.SourceGeneration.Annotations;
                    using System;
                    using System.ComponentModel.DataAnnotations;

                    namespace Acme.Clinic.Prescriptions;

                    [AttributeUsage(AttributeTargets.Property)]
                    public class ShapeAttribute : Attribute
                    {
                        public ShapeAttribute(Type type, params string[] names) { }
                        public int[] Sizes { get; set; } = [];
                    }

                    [Http(Method.Post, "api/prescriptions")]
                    public class CreatePrescription : IMessage
                    {
                        [StringLength(10, MinimumLength = 2, ErrorMessage = "bad")]
                        public string Name { get; set; } = string.Empty;

                        [Range(0.5, 2.5)]
                        public double Dose { get; set; }

                        [Shape(typeof(string), "a", "b", Sizes = new[] { 1, 2 })]
                        public string Other { get; set; } = string.Empty;
                    }

                    public static partial class Clients
                    {
                        [Generated(Http.Clients)]
                        public static partial void AddClinicHttpClients(this IServiceCollection services);
                    }
                    """)
                .AddFrameworkReferences("net10.0")
                .AddContainingAssembly<GeneratedAttribute>()
                .AddContainingAssembly<IMessage>()
                .AddContainingAssembly<HttpAttribute>()
                .AddContainingAssembly<RegularExpressionAttribute>()
                .AddContainingAssembly<IServiceCollection>()
                .AddContainingAssembly<IHttpClientFactory>()
                .AddContainingAssembly<ISender<IMessage>>()
                .AddContainingAssembly<HttpSender<IMessage>>()
                .AddContainingAssembly<System.Text.Json.JsonSerializerOptions>();

            var compilation = builder.Build("AttributeNamedArgumentTestAssembly");

            GeneratorDriver driver = CSharpGeneratorDriver.Create(new HttpClientsSourceGenerator());
            driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

            Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

            var errors = outputCompilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
            Assert.True(errors.Count == 0, string.Join("\n", errors));

            var generated = string.Join("\n", driver.GetRunResult().GeneratedTrees.Select(tree => tree.ToString()));

            Assert.Contains("MinimumLength = 2", generated);
            Assert.Contains("ErrorMessage = \"bad\"", generated);
            Assert.Contains("typeof(string)", generated);
            Assert.Contains("0.5", generated);
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = previous;
        }
    }
}
