// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Microsoft.AspNetCore.Routing;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NSail.Messaging.Annotations;
using NSail.SourceGeneration.Annotations;
using NSail.SourceGeneration.Testing;
using NSail.SourceGenerator.Generation.Http;
using NSail.SourceGenerator.TestAssets.Http;
using System.Linq;

namespace NSail.SourceGenerator.Tests.Http.Clients;

public class HttpClientsTemplateTests
{
    [Fact]
    public void Endpoint_Name_Honors_The_MetadataTemplate_Attribute()
    {
        var builder = new CompilationUnitBuilder()
            .AddSource("MetadataTemplateAttribute.cs", """
                namespace NSail.Metadata;

                [System.AttributeUsage(System.AttributeTargets.Assembly)]
                public sealed class MetadataTemplateAttribute : System.Attribute
                {
                    public MetadataTemplateAttribute(string template) => Template = template;

                    public string Template { get; }
                }
                """)
            .AddSource("WeirdClients.cs", """
                using Microsoft.Extensions.DependencyInjection;
                using NSail.SourceGeneration.Annotations;

                [assembly: NSail.Metadata.MetadataTemplate("{Root}.{Feature}.{Area}.*.{Object}")]

                namespace Batman.Ventas.Facturacion;

                public partial class WeirdClients
                {
                    [Generated(NSail.SourceGeneration.Annotations.Http.Clients)]
                    [Source(Assembly = "NSail.SourceGenerator.TestAssets")]
                    public partial void AddWeirdClients(this IServiceCollection services);
                }
                """)
            .AddFrameworkReferences("net10.0")
            .AddContainingAssembly<GeneratedAttribute>()
            .AddContainingAssembly<TestGetMessage>()
            .AddContainingAssembly<HttpAttribute>()
            .AddContainingAssembly<IEndpointRouteBuilder>();

        var compilation = builder.Build("HttpClientsTemplateTestAssembly");

        var generator = new HttpClientsSourceGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedTrees = driver.GetRunResult().GeneratedTrees;

        // Under the default template the area would bind "Ventas" (segment 1); the
        // declared template moves it to the third segment.
        Assert.Contains(generatedTrees, tree => tree.ToString().Contains("\"Facturacion\""));
    }
}
