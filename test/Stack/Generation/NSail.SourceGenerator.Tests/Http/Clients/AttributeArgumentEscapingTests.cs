// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

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
    public void A_string_attribute_argument_reaches_the_generated_body_escaped_and_the_client_compiles(
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

        var compilation = builder.Build("AttributeArgumentEscapingTestAssembly");

        var generator = new HttpClientsSourceGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        // A raw pattern like @"^\d{14}$" appears identically in the generated text whether the
        // argument is escaped correctly or not, so grepping the text cannot tell the two apart —
        // only compiling the generated tree exposes a malformed literal (CS1009 and friends).
        var errors = outputCompilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.True(errors.Count == 0, string.Join("\n", errors));

        var generated = string.Join("\n", driver.GetRunResult().GeneratedTrees.Select(tree => tree.ToString()));

        Assert.Contains("RegularExpressionAttribute(", generated);
        Assert.Contains(SymbolDisplay.FormatLiteral(expectedPattern, quote: true), generated);
    }
}
