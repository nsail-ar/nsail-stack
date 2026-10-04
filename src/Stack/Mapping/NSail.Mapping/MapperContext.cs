// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.Mapping;

/// <summary>State that outlives a single pair during one mapping run: the mappers a nested
/// member reaches for, and the identity map that terminates cycles.</summary>
public class MapperContext
{
    // Reference identity, not value: two distinct sources that happen to be equal are two
    // targets, and a back-reference is the same instance arriving twice.
    readonly Dictionary<object, object> _mapped = new(ReferenceEqualityComparer.Instance);
    readonly IMapperResolver _mappers;

    public MapperContext(IMapperResolver mappers)
    {
        ArgumentNullException.ThrowIfNull(mappers);

        _mappers = mappers;
    }

    /// <summary>Maps a nested member, reusing the target already produced for this source in
    /// this run. Without it a back-reference (child.Parent pointing at the parent being
    /// mapped) recurses until the stack ends.</summary>
    public TTarget MapMember<TSource, TTarget>(TSource source, TTarget target)
        where TSource : notnull
    {
        if (_mapped.TryGetValue(source, out var seen))
        {
            return (TTarget)seen;
        }

        // Registered before mapping, not after: the cycle is closed while the target is
        // still being filled, which is the only moment a back-reference can resolve.
        _mapped[source] = target!;

        return _mappers.Get<TSource, TTarget>().Map(source, target, this);
    }

    public bool WasMapped(object source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return _mapped.ContainsKey(source);
    }
}

/// <summary>Finds the mapper for a pair. Generated registrations fill it; the engine never
/// scans types at run time.</summary>
public interface IMapperResolver
{
    IMapper<TSource, TTarget> Get<TSource, TTarget>();
}
