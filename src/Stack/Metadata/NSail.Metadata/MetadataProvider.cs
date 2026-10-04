// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using System.Collections.Concurrent;
using System.Reflection;

namespace NSail.Metadata;

/// <summary>The single door to type metadata: the namespace convention plus the company
/// policy on top of it. The template comes from the type's assembly —
/// [assembly: MetadataTemplate(...)] when declared, MetadataTemplateAttribute.Default
/// otherwise. Override Get to patch inconsistencies or map third-party types
/// ("Google.* belongs to Mailing") — every consumer (localization keys, settings keys,
/// Swagger tags, logging, tooling) follows the same policy. Parse and Bind are protected
/// for implementations that resolve templates their own way.</summary>
public class MetadataProvider
{
    static readonly string[] _knownTokens = ["Root", "Area", "Feature", "SubFeature", "Object"];
    static readonly (string[] Left, string[] Right) _default = Parse(MetadataTemplateAttribute.Default);

    readonly ConcurrentDictionary<Type, TypeMetadata> _cache = new();
    readonly ConcurrentDictionary<Assembly, (string[] Left, string[] Right)> _templates = new();

    /// <summary>Metadata for a type, cached per type; the template comes from the type's
    /// assembly attribute when present, from the default otherwise. This is the policy
    /// door: overriding it changes what KeyFor and every consumer see.</summary>
    public virtual TypeMetadata Get(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        return _cache.GetOrAdd(type, t =>
        {
            var (left, right) = _templates.GetOrAdd(t.Assembly, static a =>
                a.GetCustomAttribute<MetadataTemplateAttribute>() is { } declared
                    ? Parse(declared.Template)
                    : _default);

            return Bind(GetFullName(t), left, right);
        });
    }

    /// <summary>Canonical key for a type: "{Area}.{Object}" plus an optional member
    /// ("Directory.PartyIdentity.Name") — used for localization and settings storage.
    /// Defined in terms of Get: overriding Get affects every derived key.</summary>
    public virtual string KeyFor(Type type, string? member = null)
    {
        var metadata = Get(type);
        var name = metadata.Object ?? type.Name;
        var key = metadata.Area is null ? name : $"{metadata.Area}.{name}";

        return member is null ? key : $"{key}.{member}";
    }

    /// <summary>Canonical key for an enum value ("Directory.PartyKind.Individual").</summary>
    public string KeyFor(Enum value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return KeyFor(value.GetType(), value.ToString());
    }

    /// <summary>The raw convention: applies the default template to a full name — no
    /// policy, no cache, no assembly attribute (a plain string has no assembly). Tokens
    /// bind left-to-right before the "*" and right-to-left after it (catch-all); tokens
    /// without a segment stay null.</summary>
    public TypeMetadata FromName(string fullName)
    {
        ArgumentException.ThrowIfNullOrEmpty(fullName);

        return Bind(fullName, _default.Left, _default.Right);
    }

    protected static (string[] Left, string[] Right) Parse(string template)
    {
        ArgumentException.ThrowIfNullOrEmpty(template);

        var left = new List<string>();
        var right = new List<string>();
        var seen = new HashSet<string>();
        var afterStar = false;

        foreach (var part in template.Split('.'))
        {
            if (part == "*")
            {
                if (afterStar)
                {
                    throw new ArgumentException($"Template '{template}' has more than one '*'.");
                }

                afterStar = true;
                continue;
            }

            if (part.Length < 3 || part[0] != '{' || part[^1] != '}')
            {
                throw new ArgumentException($"Template '{template}' has an invalid part '{part}'.");
            }

            var token = part[1..^1];

            if (!_knownTokens.Contains(token))
            {
                throw new ArgumentException($"Template '{template}' has an unknown token '{token}'.");
            }

            if (!seen.Add(token))
            {
                throw new ArgumentException($"Template '{template}' repeats the token '{token}'.");
            }

            (afterStar ? right : left).Add(token);
        }

        return (left.ToArray(), right.ToArray());
    }

    protected static TypeMetadata Bind(string fullName, string[] left, string[] right)
    {
        var segments = fullName.Split('.', StringSplitOptions.RemoveEmptyEntries);
        var values = new Dictionary<string, string>();

        var boundFromRight = Math.Min(right.Length, segments.Length);

        for (var i = 0; i < boundFromRight; i++)
        {
            values[right[^(i + 1)]] = segments[^(i + 1)];
        }

        var remaining = segments.Length - boundFromRight;

        for (var i = 0; i < Math.Min(left.Length, remaining); i++)
        {
            values[left[i]] = segments[i];
        }

        return new TypeMetadata
        {
            Root = values.GetValueOrDefault("Root"),
            Area = values.GetValueOrDefault("Area"),
            Feature = values.GetValueOrDefault("Feature"),
            SubFeature = values.GetValueOrDefault("SubFeature"),
            Object = values.GetValueOrDefault("Object"),
        };
    }

    static string GetFullName(Type type)
    {
        var name = type.Name;
        var arity = name.IndexOf('`');

        if (arity >= 0)
        {
            name = name[..arity];
        }

        return type.Namespace is null ? name : $"{type.Namespace}.{name}";
    }
}
