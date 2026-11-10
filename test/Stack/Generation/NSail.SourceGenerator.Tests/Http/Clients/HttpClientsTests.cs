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
using NSail.SourceGenerator.TestAssets.Http;
using System.Linq;

namespace NSail.SourceGenerator.Tests.Http.Clients;

public class HttpClientsTests
{
    [Fact]
    public void Clients_Generated()
    {
        var builder = new CompilationUnitBuilder()
            .AddSourceFiles("./Http/Clients/Sources/**/*.cs")
            .AddFrameworkReferences()
            .AddContainingAssembly<GeneratedAttribute>()
            .AddContainingAssembly<TestGetMessage>()
            .AddContainingAssembly<HttpAttribute>()
            .AddContainingAssembly<IEndpointRouteBuilder>()
            .AddContainingAssembly<Microsoft.Extensions.DependencyInjection.IServiceCollection>()
            .AddContainingAssembly<System.Net.Http.IHttpClientFactory>()
            .AddContainingAssembly<NSail.Messaging.IMessage>()
            .AddContainingAssembly<NSail.Messaging.Runtime.Sending.ISender<NSail.Messaging.IMessage>>()
            .AddContainingAssembly<NSail.Messaging.Http.HttpSender<NSail.Messaging.IMessage>>()
            .AddContainingAssembly<NSail.Messaging.Runtime.Pipelines.InProcessSendPipeline<NSail.Messaging.IMessage>>()
            .AddContainingAssembly<NSail.Messaging.WebApi.IEndpointEntry>()
            .AddContainingAssembly<NSail.Metadata.MetadataProvider>()
            .AddContainingAssembly<Microsoft.AspNetCore.Mvc.FromQueryAttribute>()
            .AddContainingAssembly<Microsoft.AspNetCore.Http.HttpContext>()
            .AddContainingAssembly<System.Text.Json.JsonSerializerOptions>();

        var compilation = builder.Build("HttpClientsTestAssembly");

        var generator = new HttpClientsSourceGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
        Assert.Empty(outputCompilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));

        var runResult = driver.GetRunResult();
        var generatedTrees = runResult.GeneratedTrees;

        Assert.NotEmpty(generatedTrees);
    }

    // The whole of what a generated sender says about headers: it takes the read door and hands
    // it to HttpSender, which stamps. Emitting the loop into every sender would be the same code
    // a hundred times over for a rule that belongs to the transport, not to the message.
    [Fact]
    public void A_generated_sender_takes_the_delivery_accessor_and_stamps_nothing_itself()
    {
        var builder = new CompilationUnitBuilder()
            .AddSourceFiles("./Http/Clients/Sources/**/*.cs")
            .AddFrameworkReferences()
            .AddContainingAssembly<GeneratedAttribute>()
            .AddContainingAssembly<TestGetMessage>()
            .AddContainingAssembly<HttpAttribute>()
            .AddContainingAssembly<IEndpointRouteBuilder>();

        var compilation = builder.Build("HttpClientsTestAssembly");

        GeneratorDriver driver = CSharpGeneratorDriver.Create(new HttpClientsSourceGenerator());

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generated = string.Join("\n", driver.GetRunResult().GeneratedTrees.Select(tree => tree.ToString()));

        Assert.Contains("IHttpClientFactory httpClientFactory, MessageContextAccessor deliveries", generated);
        Assert.Contains(": base(httpClientFactory, deliveries)", generated);
        Assert.DoesNotContain("X-NSail-", generated);
    }

    [Fact]
    public void An_AsHeader_member_is_sent_as_a_named_header()
    {
        var builder = new CompilationUnitBuilder()
            .AddSourceFiles("./Http/Clients/Sources/**/*.cs")
            .AddFrameworkReferences()
            .AddContainingAssembly<GeneratedAttribute>()
            .AddContainingAssembly<TestGetMessage>()
            .AddContainingAssembly<HttpAttribute>()
            .AddContainingAssembly<IEndpointRouteBuilder>();

        GeneratorDriver driver = CSharpGeneratorDriver.Create(new HttpClientsSourceGenerator());

        driver = driver.RunGeneratorsAndUpdateCompilation(builder.Build("HttpClientsTestAssembly"), out _, out var diagnostics);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generated = string.Join("\n", driver.GetRunResult().GeneratedTrees.Select(tree => tree.ToString()));

        Assert.Contains(".AddHeader(\"trace\", message.Trace)", generated);
        Assert.Contains(".AddHeader(\"attempt\", message.Attempt)", generated);
        Assert.Contains(".AddHeader(\"tenant\", message.Tenant)", generated);
        Assert.DoesNotContain(".AddHeader(message)", generated);
    }

    [Fact]
    public void A_generated_sender_writes_each_route_value_through_Segment()
    {
        var builder = new CompilationUnitBuilder()
            .AddSourceFiles("./Http/Clients/Sources/**/*.cs")
            .AddFrameworkReferences()
            .AddContainingAssembly<GeneratedAttribute>()
            .AddContainingAssembly<TestGetMessage>()
            .AddContainingAssembly<HttpAttribute>()
            .AddContainingAssembly<IEndpointRouteBuilder>();

        var compilation = builder.Build("HttpClientsTestAssembly");

        GeneratorDriver driver = CSharpGeneratorDriver.Create(new HttpClientsSourceGenerator());

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generated = string.Join("\n", driver.GetRunResult().GeneratedTrees.Select(tree => tree.ToString()));

        Assert.Contains("api/v1/samples/external/{HttpRequestMessageBuilder.Segment(message.Id)}", generated);
        Assert.DoesNotContain("{message.Id}", generated);
    }

    [Theory]
    [InlineData("public System.Collections.Immutable.ImmutableArray<string> Tags { get; set; }")]
    [InlineData("public required System.Collections.Immutable.ImmutableArray<string> Tags { get; set; }")]
    public void A_struct_collection_in_the_query_is_sent_as_its_elements(string member)
    {
        var builder = new CompilationUnitBuilder()
            .AddSource("StructQuery.cs", $$"""
                using Microsoft.Extensions.DependencyInjection;
                using NSail.Messaging;
                using NSail.Messaging.Annotations;
                using NSail.SourceGeneration.Annotations;

                namespace Acme.Clinic.Items;

                [Http(Method.Get, "api/items")]
                public class FindItems : IMessage
                {
                    [AsQuery]
                    {{member}}
                }

                public static partial class Clients
                {
                    [Generated(Http.Clients)]
                    public static partial void AddClinicHttp(this IServiceCollection services);
                }
                """)
            .AddFrameworkReferences()
            .AddContainingAssembly<GeneratedAttribute>()
            .AddContainingAssembly<IMessage>()
            .AddContainingAssembly<HttpAttribute>();

        GeneratorDriver driver = CSharpGeneratorDriver.Create(new HttpClientsSourceGenerator());

        driver = driver.RunGeneratorsAndUpdateCompilation(builder.Build("StructQueryTestAssembly"), out _, out _);

        var generated = string.Join("\n", driver.GetRunResult().GeneratedTrees.Select(tree => tree.ToString()));

        Assert.Contains("(System.Collections.Generic.IEnumerable<string>?)message.Tags", generated);
    }
}
