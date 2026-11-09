// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using NSail.SourceGenerator.Roslyn;

namespace NSail.SourceGenerator.Generation;

/// <summary>A <c>[Source]</c> declaration on a holder: where its scope reaches beyond the
/// assembly being compiled.</summary>
public sealed record SourceScope(string? Assembly, string? Pattern);

/// <summary>A <c>[Generated]</c> partial method as plain data: everything a generator needs of
/// the holder, with no symbol behind it.</summary>
public sealed record GeneratorTarget(
    string Namespace,
    string ClassName,
    string MethodName,
    string ClassDeclaration,
    string MethodDeclaration,
    string FirstParameterName,
    string ServicesParameterName,
    string? FilePath,
    TextSpan Span,
    EquatableList<SourceScope> Sources);

/// <summary>What a generator found in scope for one holder.</summary>
public sealed record Scoped<T>(GeneratorTarget Target, EquatableList<T> Fragments);

public static class GeneratedSyntaxExtensions
{
    private const string GeneratedAttributeName = "NSail.SourceGeneration.Annotations.GeneratedAttribute";
    private const string SourceAttributeName = "NSail.SourceGeneration.Annotations.SourceAttribute";
    private const string MetadataTemplateAttributeName = "NSail.Metadata.MetadataTemplateAttribute";

    public static IncrementalValuesProvider<GeneratorTarget> FindGeneratedTargets(this IncrementalGeneratorInitializationContext context, int outputType)
    {
        return context.SyntaxProvider
            .ForAttributeWithMetadataName(
                GeneratedAttributeName,
                static (node, _) => node is MethodDeclarationSyntax,
                (ctx, _) => ToTarget(ctx, outputType)
            )
            .Where(static target => target is not null)
            .Select(static (target, _) => target!)
            .WithTrackingName("Targets");
    }

    /// <summary>The <c>[assembly: MetadataTemplate]</c> of the compilation, or null.</summary>
    public static IncrementalValueProvider<string?> FindMetadataTemplate(this IncrementalGeneratorInitializationContext context)
    {
        return context.SyntaxProvider
            .ForAttributeWithMetadataName(
                MetadataTemplateAttributeName,
                static (_, _) => true,
                static (ctx, _) => ctx.Attributes[0].ConstructorArguments.Length > 0
                    ? ctx.Attributes[0].ConstructorArguments[0].Value as string
                    : null)
            .Collect()
            .Select(static (templates, _) => templates.FirstOrDefault())
            .WithTrackingName("MetadataTemplate");
    }

    static GeneratorTarget? ToTarget(GeneratorAttributeSyntaxContext ctx, int outputType)
    {
        if (ctx.TargetSymbol is not IMethodSymbol method)
        {
            return null;
        }

        var generated = ctx.Attributes.FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == GeneratedAttributeName);

        if (generated is null || generated.GetConstructorValue<int>(0) != outputType)
        {
            return null;
        }

        return ToTarget(method, ctx.TargetNode);
    }

    public static GeneratorTarget ToTarget(IMethodSymbol method, SyntaxNode? declaration = null)
    {
        declaration ??= method.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax();

        var sources = new EquatableList<SourceScope>(method.GetAttributes(SourceAttributeName)
            .Select(a => new SourceScope(a.GetNamedArgument<string>("Assembly"), a.GetNamedArgument<string>("Pattern"))));

        return new GeneratorTarget(
            method.ContainingNamespace.ToDisplayString(),
            method.ContainingType.Name,
            method.Name,
            method.ContainingType.GetDeclaration(),
            method.GetDeclaration(),
            method.Parameters.Length > 0 ? method.Parameters[0].Name : "services",
            method.FindParameterNameByType("IServiceCollection"),
            declaration?.SyntaxTree.FilePath,
            declaration?.Span ?? default,
            sources);
    }

    /// <summary>Inspects every class of the assembly from its own declaration, so an edit
    /// re-reads the types of the edited file alone; a holder with <c>[Source]</c> reads the
    /// scopes it names from the compilation instead — the one step that depends on it, and one
    /// whose result is compared by value, so nothing after it runs when it is unchanged.</summary>
    public static IncrementalValuesProvider<Scoped<T>> GatherInScope<T>(
        this IncrementalGeneratorInitializationContext context,
        IncrementalValuesProvider<GeneratorTarget> targets,
        Func<INamedTypeSymbol, T?> inspect)
        where T : class
    {
        var local = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => node is ClassDeclarationSyntax or RecordDeclarationSyntax,
                (ctx, ct) =>
                {
                    if (ctx.SemanticModel.GetDeclaredSymbol(ctx.Node, ct) is not INamedTypeSymbol type
                        || !CompilationExtensions.IsInDefaultScope(type)
                        || !IsFirstDeclaration(type, ctx.Node))
                    {
                        return null;
                    }

                    return inspect(type);
                })
            .Where(static fragment => fragment is not null)
            .Collect()
            .Select(static (fragments, _) => new EquatableList<T>(fragments!))
            .WithTrackingName("LocalTypes");

        var referenced = targets
            .Combine(context.CompilationProvider)
            .Select((pair, ct) =>
            {
                var (target, compilation) = pair;
                var fragments = new EquatableList<T>();

                if (target.Sources.Count > 0)
                {
                    foreach (var type in CompilationExtensions.FindTypesInSources(compilation, target.Sources))
                    {
                        ct.ThrowIfCancellationRequested();

                        if (inspect(type) is { } fragment)
                        {
                            fragments.Add(fragment);
                        }
                    }
                }

                return new Scoped<T>(target, fragments);
            })
            .WithTrackingName("SourceTypes");

        return referenced
            .Combine(local)
            .Select(static (pair, _) =>
            {
                var (found, fragments) = pair;

                return found.Target.Sources.Count > 0 ? found : new Scoped<T>(found.Target, fragments);
            })
            .WithTrackingName("Scoped");
    }

    // A partial class is declared more than once and its symbol is the same each time.
    static bool IsFirstDeclaration(INamedTypeSymbol type, SyntaxNode node)
    {
        var first = type.DeclaringSyntaxReferences.FirstOrDefault();

        return first is not null && first.SyntaxTree == node.SyntaxTree && first.Span == node.Span;
    }

    /// <summary>The holder's method, found again in a compilation: the mapper emission walks
    /// symbols, so it needs the one thing a cached target cannot hold.</summary>
    public static IMethodSymbol? ResolveMethod(this GeneratorTarget target, Compilation compilation)
    {
        foreach (var tree in compilation.SyntaxTrees)
        {
            if (tree.FilePath != target.FilePath || target.Span.End > tree.Length)
            {
                continue;
            }

            if (tree.GetRoot().FindNode(target.Span) is MethodDeclarationSyntax declaration
                && declaration.Identifier.ValueText == target.MethodName)
            {
                return compilation.GetSemanticModel(tree).GetDeclaredSymbol(declaration) as IMethodSymbol;
            }
        }

        return null;
    }
}
