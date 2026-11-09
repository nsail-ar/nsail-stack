// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NSail.SourceGenerator.Roslyn;

namespace NSail.SourceGenerator.Generation.Mappers;

/// <summary>The file one holder emits, as data: what is registered with the compiler is the
/// text and the findings, so an edit that leaves them as they were adds nothing.</summary>
public sealed record MapperOutput(string HintName, string Source, EquatableList<DiagnosticInfo> Diagnostics);

[Generator]
public class MappersSourceGenerator : IIncrementalGenerator
{
    const string ModelBuilderName = "Microsoft.EntityFrameworkCore.ModelBuilder";
    const string DbContextName = "Microsoft.EntityFrameworkCore.DbContext";
    const string DbSetName = "Microsoft.EntityFrameworkCore.DbSet<TEntity>";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var targets = context.FindGeneratedTargets(outputType: TargetIds.Mappers_Entities);

        // The mappers read every type reachable from the declarations in scope, so the emission
        // itself needs the compilation; what it is compared on is its output.
        var outputs = targets
            .Combine(FindModelled(context))
            .Combine(context.CompilationProvider)
            .Select(static (pair, ct) =>
            {
                var ((target, modelled), compilation) = pair;
                var holder = new HolderCheck(target);

                if (holder.Unusable)
                {
                    return new MapperOutput(string.Empty, string.Empty, holder.HolderDiagnostics);
                }

                var method = target.ResolveMethod(compilation);

                if (method is null)
                {
                    return null;
                }

                var emitter = new MapperEmitter(method, compilation, modelled);
                var source = emitter.Emit();

                return new MapperOutput(
                    $"{target.Namespace}.{target.ClassName}_{target.MethodName}.g.cs",
                    source,
                    new EquatableList<DiagnosticInfo>(emitter.Diagnostics));
            })
            .Where(static output => output is not null)
            .WithTrackingName("Mappers");

        context.RegisterSourceOutput(outputs, static (spc, output) =>
        {
            foreach (var diagnostic in output!.Diagnostics)
            {
                diagnostic.Report(spc);
            }

            if (output.HintName.Length > 0)
            {
                spc.AddSource(output.HintName, output.Source);
            }
        });
    }

    // The entities the store's model declares in this assembly, found where they are written:
    // a ModelBuilder.Entity<T>() call or a DbSet<T> property of a context.
    static IncrementalValueProvider<EquatableList<string>> FindModelled(IncrementalGeneratorInitializationContext context)
    {
        var calls = context.SyntaxProvider.CreateSyntaxProvider(
            static (node, _) => node is GenericNameSyntax { Identifier.ValueText: "Entity", TypeArgumentList.Arguments.Count: 1 },
            static (ctx, ct) =>
            {
                if (ctx.SemanticModel.GetSymbolInfo(ctx.Node, ct).Symbol is IMethodSymbol { ContainingType: { } builder } method
                    && builder.ToDisplayString() == ModelBuilderName
                    && method.TypeArguments[0] is INamedTypeSymbol entity)
                {
                    return MetadataKey(entity.OriginalDefinition);
                }

                return null;
            });

        var sets = context.SyntaxProvider.CreateSyntaxProvider(
            static (node, _) => node is ClassDeclarationSyntax,
            static (ctx, ct) =>
            {
                var names = new EquatableList<string>();

                if (ctx.SemanticModel.GetDeclaredSymbol(ctx.Node, ct) is INamedTypeSymbol type && type.InheritsFrom(DbContextName))
                {
                    foreach (var set in type.GetMembers().OfType<IPropertySymbol>())
                    {
                        if (set.Type is INamedTypeSymbol { IsGenericType: true } named
                            && named.OriginalDefinition.ToDisplayString() == DbSetName
                            && named.TypeArguments[0] is INamedTypeSymbol entity)
                        {
                            names.Add(MetadataKey(entity.OriginalDefinition));
                        }
                    }
                }

                return names;
            });

        return calls.Where(static name => name is not null).Select(static (name, _) => new EquatableList<string> { name! })
            .Collect()
            .Combine(sets.Collect())
            .Select(static (pair, _) =>
            {
                var names = new SortedSet<string>(StringComparer.Ordinal);

                foreach (var list in pair.Left)
                {
                    names.UnionWith(list);
                }

                foreach (var list in pair.Right)
                {
                    names.UnionWith(list);
                }

                return new EquatableList<string>(names);
            })
            .WithTrackingName("Modelled");
    }

    static string MetadataKey(INamedTypeSymbol type)
    {
        var name = type.MetadataName;

        for (var outer = type.ContainingType; outer is not null; outer = outer.ContainingType)
        {
            name = outer.MetadataName + "+" + name;
        }

        return type.ContainingNamespace.IsGlobalNamespace ? name : type.ContainingNamespace.ToDisplayString() + "." + name;
    }
}
