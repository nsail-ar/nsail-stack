// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.DependencyInjection;
using NSail.SourceGeneration.Annotations;

namespace NSail.Mapping.Tests.Fixtures;

/// <summary>The holder a kit's Data project declares: every [MapFrom] and [MapTo] in this assembly
/// gets its mapper or projection registered here.</summary>
public static partial class Mappings
{
    [Generated(Mappers.Entities)]
    public static partial void AddTestMappings(this IServiceCollection services);

    static readonly ServiceProvider Provider = Build();

    /// <summary>The generated mapper for a pair, as a handler would resolve it.</summary>
    public static IEntityMapper<TSource, TTarget> For<TSource, TTarget>()
        where TTarget : class
    {
        return Provider.GetRequiredService<IEntityMapper<TSource, TTarget>>();
    }

    /// <summary>The Detached shape: map onto a target in hand, through a context.</summary>
    public static TTarget Map<TSource, TTarget>(TSource source, TTarget? target = null, MapContext? context = null)
        where TTarget : class
    {
        return For<TSource, TTarget>().Map(source, target, context ?? new MapContext(), CancellationToken.None).GetAwaiter().GetResult();
    }

    /// <summary>The generated projection for a pair, compiled: Detached's Bind(...).Compile().</summary>
    public static Func<TSource, TTarget> Projected<TSource, TTarget>()
    {
        return Provider.GetRequiredService<IProjection<TSource, TTarget>>().Expression.Compile();
    }

    static ServiceProvider Build()
    {
        var services = new ServiceCollection();

        services.AddTestMappings();

        return services.BuildServiceProvider();
    }
}
