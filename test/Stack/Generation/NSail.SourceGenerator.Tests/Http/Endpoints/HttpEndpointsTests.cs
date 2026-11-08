// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.AspNetCore.Routing;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NSail.Messaging.Annotations;
using NSail.SourceGeneration.Annotations;
using NSail.SourceGeneration.Testing;
using NSail.SourceGenerator.Generation.Http;
using NSail.SourceGenerator.TestAssets.Http;
using System;
using System.Linq;

namespace NSail.SourceGenerator.Tests.Http.Endpoints;

public class HttpEndpointsTests
{
    [Fact]
    public void HttpEndpoints_Generate()
    {
        var builder = new CompilationUnitBuilder()
            .AddSourceFiles("./Http/Endpoints/Sources/**/*.cs")
            .AddFrameworkReferences("net10.0")
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
            .AddContainingAssembly<Microsoft.AspNetCore.Http.HttpResults.Ok>()
            .AddContainingAssembly<Microsoft.AspNetCore.Routing.RouteData>()
            .AddContainingAssembly<System.Text.Json.JsonSerializerOptions>();

        var compilation = builder.Build("HttpEndpointsTestAssembly");

        var generator = new HttpEndpointsSourceGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
        Assert.Empty(outputCompilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));

        var runResult = driver.GetRunResult();
        var generatedTrees = runResult.GeneratedTrees;

        Assert.NotEmpty(generatedTrees);
    }

    [Fact]
    public void HttpEndpoints_Bind_defaulted_query_parameters_as_optional()
    {
        var source = GenerateEntry("TestPagedMessage");

        // A required property has no default to fall back on, so it stays a required parameter.
        Assert.Contains("[FromQuery] System.Guid ownerid,", source);
        Assert.Contains("OwnerId = ownerid,", source);

        Assert.Contains("[FromQuery] int? pageindex,", source);
        Assert.Contains("[FromQuery] int? pagesize,", source);
        Assert.Contains("[FromQuery] string? search,", source);

        // The message keeps its own declared default whenever the query omits the parameter.
        Assert.DoesNotContain("PageSize = pagesize,", source);
        Assert.Contains("if (pagesize is not null)", source);
        Assert.Contains("message.PageSize = pagesize.Value;", source);
        Assert.Contains("if (search is not null)", source);
        Assert.Contains("message.Search = search;", source);
    }

    // The receive half of a delivery's context: the entry binds the request itself — minimal
    // APIs hand an HttpContext over with no attribute — and hands the pipeline the headers that
    // arrived, where it used to hand it null. The parameter is named apart from anything a
    // message can bind, since the two share one signature.
    [Theory]
    [InlineData("TestGetMessage")]
    [InlineData("TestVoidMessage")]
    public void HttpEndpoints_hand_the_pipeline_the_headers_the_request_arrived_with(string messageName)
    {
        var source = GenerateEntry(messageName);

        Assert.Contains("HttpContext nsailDelivery,", source);
        Assert.Contains("pipeline.Execute(message, DeliveryHeaders.Read(nsailDelivery), cancellationToken)", source);
        Assert.DoesNotContain("pipeline.Execute(message, null, cancellationToken)", source);
    }

    [Theory]
    [InlineData("TestHeadMessage", "HEAD")]
    [InlineData("TestOptionsMessage", "OPTIONS")]
    public void HttpEndpoints_Map_head_and_options_through_MapMethods(string messageName, string verb)
    {
        var source = GenerateEntry(messageName);

        Assert.Contains("builder.MapMethods(", source);
        Assert.Contains($"new[] {{ \"{verb}\" }}", source);
        Assert.DoesNotContain("builder.MapHead(", source);
        Assert.DoesNotContain("builder.MapOptions(", source);
    }

    static string GenerateEntry(string messageName)
    {
        var builder = new CompilationUnitBuilder()
            .AddSourceFiles("./Http/Endpoints/Sources/**/*.cs")
            .AddFrameworkReferences("net10.0")
            .AddContainingAssembly<GeneratedAttribute>()
            .AddContainingAssembly<TestGetMessage>()
            .AddContainingAssembly<HttpAttribute>()
            .AddContainingAssembly<IEndpointRouteBuilder>();

        var compilation = builder.Build("HttpEndpointsTestAssembly");

        GeneratorDriver driver = CSharpGeneratorDriver.Create(new HttpEndpointsSourceGenerator());

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var tree = driver.GetRunResult().GeneratedTrees
            .Single(t => t.FilePath.Contains($"{messageName}_", StringComparison.Ordinal));

        return tree.ToString();
    }
}