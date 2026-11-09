// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Linq.Expressions;
using NSail.Mapping.Tests.Fixtures;

namespace NSail.Mapping.Tests.Entity;

public sealed class MapRootLoad
{
    [Fact]
    public void a_root_is_loaded_by_its_type_and_key_never_by_a_compiled_match()
    {
        var context = new KeyedLoadContext(new MapParameters { MissingRootBehavior = MissingRootBehavior.Create });

        for (var i = 1; i <= 50; i++)
        {
            Mappings.Map<MapAggregation.SourceEntity, MapAggregation.TargetEntity>(new MapAggregation.SourceEntity { Id = i, Name = "s" + i }, null, context);
        }

        Assert.Equal(50, context.KeyedLoads);
        Assert.Equal(0, context.MatchLoads);
    }

    sealed class KeyedLoadContext : MapContext
    {
        public KeyedLoadContext(MapParameters parameters)
            : base(parameters)
        {
        }

        public int KeyedLoads { get; private set; }

        public int MatchLoads { get; private set; }

        public override bool Stores
        {
            get { return true; }
        }

        public override Task<TEntity?> Load<TEntity>(Expression<Func<TEntity, bool>> match, IReadOnlyList<string> includes, CancellationToken cancellationToken)
            where TEntity : class
        {
            MatchLoads++;

            return Task.FromResult<TEntity?>(null);
        }

        public override Task<TEntity?> Load<TEntity>(EntityType<TEntity> type, object key, IReadOnlyList<string> includes, CancellationToken cancellationToken)
            where TEntity : class
        {
            KeyedLoads++;

            return Task.FromResult<TEntity?>(null);
        }
    }
}
