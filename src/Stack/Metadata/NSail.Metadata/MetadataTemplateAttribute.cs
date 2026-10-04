// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.Metadata;

/// <summary>Declares the namespace template for one assembly's types — the single source
/// of truth for the convention, readable by source generators from the compilation and by
/// MetadataProvider at runtime, so both sides derive the same metadata. Assemblies
/// without it follow Default: foreign code with a different shape coexists with
/// NSail-shaped kits and products.</summary>
[AttributeUsage(AttributeTargets.Assembly)]
public sealed class MetadataTemplateAttribute(string template) : Attribute
{
    public const string Default = "{Root}.{Area}.{Feature}.*.{Object}";

    public string Template { get; } = template;
}
