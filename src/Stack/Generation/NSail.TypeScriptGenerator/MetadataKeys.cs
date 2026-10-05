// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.CodeAnalysis;
using NSail.Metadata;

namespace NSail.TypeScriptGenerator;

// MetadataProvider's convention, applied to a symbol: the template is the declaring
// assembly's [assembly: MetadataTemplate] or the default, bound by the provider's own
// Parse/Bind, so a key here is the key KeyFor renders for the same type at runtime.
sealed class MetadataKeys : MetadataProvider
{
    const string TemplateAttribute = "NSail.Metadata.MetadataTemplateAttribute";

    readonly Dictionary<IAssemblySymbol, (string[] Left, string[] Right)> _templates = new(SymbolEqualityComparer.Default);

    public TypeMetadata Get(INamedTypeSymbol type)
    {
        var (left, right) = Template(type.ContainingAssembly);
        var space = type.ContainingNamespace is { IsGlobalNamespace: false } ns ? ns.ToDisplayString() + "." : string.Empty;

        return Bind(space + type.Name, left, right);
    }

    public string KeyFor(INamedTypeSymbol type, string? member = null)
    {
        var metadata = Get(type);
        var name = metadata.Object ?? type.Name;
        var key = metadata.Area is null ? name : $"{metadata.Area}.{name}";

        return member is null ? key : $"{key}.{member}";
    }

    (string[] Left, string[] Right) Template(IAssemblySymbol assembly)
    {
        if (_templates.TryGetValue(assembly, out var cached))
        {
            return cached;
        }

        var declared = assembly.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == TemplateAttribute)?
            .ConstructorArguments.FirstOrDefault().Value as string;

        var template = Parse(declared ?? MetadataTemplateAttribute.Default);

        _templates[assembly] = template;

        return template;
    }
}
