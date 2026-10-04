// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.DependencyInjection;
using NSail.Injection.Annotations;
using NSail.SourceGeneration.Annotations;
using NSail.SourceGeneration.Testing;
using NSail.SourceGenerator.Generation.Services;
using NSail.SourceGenerator.TestAssets.Services;
using System.Linq;

namespace NSail.SourceGenerator.Tests.Services.Registration;

public class ServiceRegistrationTests
{
    [Fact]
    public void AddServices_Generate()
    {
        var builder = new CompilationUnitBuilder()
            .AddSourceFiles("./Services/Registration/Sources/**/*.cs")
            .AddFrameworkReferences("net10.0")
            .AddContainingAssembly<GeneratedAttribute>()
            .AddContainingAssembly<InjectableAttribute>()
            .AddContainingAssembly<ServiceB>()
            .AddContainingAssembly<ServiceLifetime>();

        var compilation = builder.Build("ServiceRegistrationTestAssembly");

        var generator = new ServiceRegistrationSourceGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var runResult = driver.GetRunResult();
        var generatedTrees = runResult.GeneratedTrees;

        Assert.NotEmpty(generatedTrees);
    }
}