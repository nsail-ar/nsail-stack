// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using NSail.Metadata;

namespace NSail.Messaging.Runtime;

/// <summary>Names every message the composition knows and answers the name back as its CLR
/// type. Naming a message is messaging's own vocabulary: a replay that carries a
/// discriminator plus a payload, and a policy that grants a key, ask the same question here
/// instead of each building a registry of its own.</summary>
public class MessageRegistry
{
    readonly MetadataProvider _metadata;
    readonly Dictionary<string, Type> _types;
    readonly List<string> _keys;

    public MessageRegistry(MetadataProvider metadata, IEnumerable<Type> messageTypes)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(messageTypes);

        _metadata = metadata;
        _types = messageTypes.ToDictionary(KeyFor);
        _keys = _types.Keys.Order(StringComparer.Ordinal).ToList();
    }

    /// <summary>Every key the registry knows, ordered — what a policy editor builds its tree
    /// from, so the checklist can only offer keys a message exists for.</summary>
    public IReadOnlyList<string> Keys => _keys;

    public Type? Resolve(string key)
    {
        return _types.GetValueOrDefault(key);
    }

    /// <summary>The permissions rendering of the metadata: "{Area}.{Feature}.{Object}" —
    /// wildcards and the editor tree group on it, so it must include the feature (KeyFor on
    /// the provider is the localization rendering and doesn't).</summary>
    public string KeyFor(Type messageType)
    {
        var m = _metadata.Get(messageType);

        return string.Join('.', new[] { m.Area, m.Feature, m.Object ?? messageType.Name }.Where(s => s is not null));
    }
}
