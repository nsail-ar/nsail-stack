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

public class HttpBodyOptionalTests
{
    [Fact]
    public void A_non_required_value_type_body_member_is_nullable_on_the_dto()
    {
        var dto = Generated("TestBodyDefaultsMessageBody");

        // Declared non-nullable, an omitted member arrives as 0 or false — indistinguishable
        // from a caller who sent them, which is what the widening removes.
        Assert.Contains("int? Retries", dto);
        Assert.Contains("bool? Active", dto);

        // A required member has no default to protect, so it stays as the message declares it.
        Assert.Contains("int Sequence", dto);
        Assert.DoesNotContain("int? Sequence", dto);

        // The display format drops the annotation on a reference type, so it is re-marked or
        // the member comes back non-nullable and cannot hold "absent".
        Assert.Contains("string? Notes", dto);
    }

    [Fact]
    public void A_required_message_member_stays_required_on_the_dto()
    {
        var dto = Generated("TestBodyDefaultsMessageBody");

        Assert.Contains("public required string Title", dto);
        Assert.Contains("public required int Sequence", dto);

        // "required" already answers for initialization, and a placeholder would contradict it.
        Assert.DoesNotContain("required string Title { get; set; } = default!;", dto);
    }

    [Fact]
    public void An_omitted_annotated_reference_body_member_is_never_assigned_onto_the_message()
    {
        var entry = Generated("TestBodyDefaultsMessage_");

        Assert.DoesNotContain("Notes = body.Notes,", entry);
        Assert.Contains("if (body.Notes is not null)", entry);
        Assert.Contains("message.Notes = body.Notes;", entry);
    }

    [Fact]
    public void An_omitted_value_type_body_member_is_never_assigned_onto_the_message()
    {
        var entry = Generated("TestBodyDefaultsMessage_");

        Assert.DoesNotContain("Retries = body.Retries,", entry);
        Assert.DoesNotContain("Active = body.Active,", entry);

        Assert.Contains("if (body.Retries is not null)", entry);
        Assert.Contains("message.Retries = body.Retries.Value;", entry);
        Assert.Contains("if (body.Active is not null)", entry);
        Assert.Contains("message.Active = body.Active.Value;", entry);

        // Already nullable: the DTO type is unchanged, but the assignment is still conditional
        // so the message's own declaration survives an omitted member.
        Assert.Contains("if (body.ExpiresOn is not null)", entry);
        Assert.Contains("message.ExpiresOn = body.ExpiresOn;", entry);

        // The reference-type rule the earlier story established is untouched.
        Assert.Contains("if (body.Tags is not null)", entry);
        Assert.Contains("message.Tags = body.Tags;", entry);

        Assert.Contains("Title = body.Title,", entry);
        Assert.Contains("Sequence = body.Sequence,", entry);
    }

    static string Generated(string fileNameFragment)
    {
        var compilation = new CompilationUnitBuilder()
            .AddSourceFiles("./Http/Endpoints/Sources/**/*.cs")
            .AddFrameworkReferences("net10.0")
            .AddContainingAssembly<GeneratedAttribute>()
            .AddContainingAssembly<TestGetMessage>()
            .AddContainingAssembly<HttpAttribute>()
            .AddContainingAssembly<IEndpointRouteBuilder>()
            .Build("HttpBodyOptionalTestAssembly");

        GeneratorDriver driver = CSharpGeneratorDriver.Create(new HttpEndpointsSourceGenerator());

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var tree = driver.GetRunResult().GeneratedTrees
            .Single(t => t.FilePath.Contains(fileNameFragment, StringComparison.Ordinal));

        return tree.ToString();
    }
}
