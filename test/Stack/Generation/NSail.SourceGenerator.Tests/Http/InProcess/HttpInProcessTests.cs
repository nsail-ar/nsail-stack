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
using System.Linq;

namespace NSail.SourceGenerator.Tests.Http.InProcess;

public class HttpInProcessTests
{
    [Fact]
    public void InProcessClients_Generated()
    {
        var builder = new CompilationUnitBuilder()
            .AddSourceFiles("./Http/InProcess/Sources/**/*.cs")
            .AddFrameworkReferences("net10.0")
            .AddContainingAssembly<GeneratedAttribute>()
            .AddContainingAssembly<TestGetMessage>()
            .AddContainingAssembly<HttpAttribute>()
            .AddContainingAssembly<IEndpointRouteBuilder>();

        var compilation = builder.Build("HttpInProcessTestAssembly");

        var generator = new HttpInProcessSourceGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var runResult = driver.GetRunResult();
        var generatedTrees = runResult.GeneratedTrees;

        Assert.NotEmpty(generatedTrees);

        var generatedCode = string.Join("\n", generatedTrees.Select(t => t.ToString()));

        // [Http] message with a result: ISender<M, R> -> InProcessSender<M, R>
        Assert.Contains("InProcessSender<NSail.SourceGenerator.TestAssets.Http.TestGetMessage, System.Collections.Generic.List<NSail.SourceGenerator.TestAssets.Http.ExternalGetResult>>", generatedCode);

        // [Http] message without a result: ISender<M> -> InProcessSender<M>
        Assert.Contains("InProcessSender<NSail.SourceGenerator.TestAssets.Http.TestVoidMessage>", generatedCode);

        // A message without [Http] gets no sender here — the transport attribute is the gate.
        Assert.DoesNotContain("TestCommand", generatedCode);
    }
}
