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

public class HttpClientsTests
{
    [Fact]
    public void Clients_Generated()
    {
        var builder = new CompilationUnitBuilder()
            .AddSourceFiles("./Http/Clients/Sources/**/*.cs")
            .AddFrameworkReferences("net10.0")
            .AddContainingAssembly<GeneratedAttribute>()
            .AddContainingAssembly<TestGetMessage>()
            .AddContainingAssembly<HttpAttribute>()
            .AddContainingAssembly<IEndpointRouteBuilder>();

        var compilation = builder.Build("HttpClientsTestAssembly");

        var generator = new HttpClientsSourceGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

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
            .AddFrameworkReferences("net10.0")
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
}
