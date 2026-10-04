// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Microsoft.CodeAnalysis;
using System.Text;

namespace NSail.SourceGenerator.Roslyn;

public static class PropertySymbolExtensions
{
    /// <summary>Renders the property as a declaration. <paramref name="typeOverride"/> replaces
    /// the declared type, which a generated DTO uses to widen a member so it can hold "absent".</summary>
    public static string GetDeclaration(this IPropertySymbol prop, string? typeOverride = null)
    {
        var sb = new StringBuilder();

        var propAttrs = prop.GetAttributeDeclarations();
        if (propAttrs.Length > 0)
        {
            sb.Append(propAttrs.TrimEnd());
            sb.AppendLine();
        }

        var modifiers = new List<string>();

        if (prop.DeclaredAccessibility != Accessibility.NotApplicable)
        {
            modifiers.Add(prop.DeclaredAccessibility.ToString().ToLowerInvariant());
        }

        if (prop.IsStatic) 
            modifiers.Add("static");

        if (prop.IsAbstract) 
            modifiers.Add("abstract");
        else if (prop.IsVirtual) 
            modifiers.Add("virtual");
        else if (prop.IsOverride) 
            modifiers.Add("override");

        if (prop.IsPartialDefinition)
            modifiers.Add("partial");

        if (prop.IsRequired)
            modifiers.Add("required");

        if (modifiers.Count > 0)
        {
            sb.Append(string.Join(" ", modifiers)).Append(' ');
        }

        sb.Append(typeOverride ?? prop.Type.ToDisplayString())
          .Append(' ')
          .Append(prop.Name)
          .Append(" { ");

        var anyAccessor = false;

        if (prop.GetMethod != null)
        {
            AppendAccessor(sb, prop.GetMethod, "get", ref anyAccessor);
        }

        if (prop.SetMethod != null)
        {
            AppendAccessor(sb, prop.SetMethod, "set", ref anyAccessor);
        }

        if (sb.Length > 0 && sb[sb.Length - 1] == ' ')
        {
            sb.Length--;
        }

        sb.Append(" }");

        // A generated DTO is filled by the deserializer, never by a constructor, so a
        // non-nullable reference property would report as uninitialized on every build.
        if (NeedsDefault(prop, typeOverride))
        {
            sb.Append(" = default!;");
        }

        return sb.ToString().TrimEnd();
    }

    static bool NeedsDefault(IPropertySymbol prop, string? typeOverride)
    {
        // A required member is initialized by every caller the compiler accepts, so it needs no
        // placeholder — and an initializer on one is not what the declaration means.
        if (prop.IsAbstract || prop.IsPartialDefinition || prop.IsRequired || !prop.Type.IsReferenceType)
        {
            return false;
        }

        if (typeOverride is not null)
        {
            return !typeOverride.EndsWith("?", StringComparison.Ordinal);
        }

        return prop.Type.NullableAnnotation == NullableAnnotation.NotAnnotated;
    }

    static void AppendAccessor(
        StringBuilder sb,
        IMethodSymbol accessor,
        string keyword,
        ref bool anyAccessor)
    {
        if (!anyAccessor)
        {
            anyAccessor = true;
        }

        var attrs = accessor.GetAttributeDeclarations();
        if (attrs.Length > 0)
        {
            sb.Append(attrs);
        }

        if (accessor.DeclaredAccessibility != accessor.ContainingSymbol.DeclaredAccessibility)
        {
            sb.Append(accessor.DeclaredAccessibility.ToString().ToLowerInvariant())
              .Append(' ');
        }

        sb.Append(keyword).Append("; ");
    }
}