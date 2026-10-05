// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Mapping.Tests.Fixtures;

public enum MapAction
{
    Create,
    Update,
    Delete,
    Attach,
}

/// <summary>Detached's MapContextMock: a context with no store that records what the mapper
/// decided for each entity, by type and key, so a test reads the graph's verdicts.</summary>
public sealed class RecordingContext : MapContext
{
    readonly Dictionary<(Type, object?), MapAction> _actions = [];

    public RecordingContext(MapParameters? parameters = null)
        : base(parameters)
    {
    }

    public bool Verify<TEntity>(object key, out MapAction action)
    {
        return _actions.TryGetValue((typeof(TEntity), key), out action);
    }

    public override Task<TEntity?> Attach<TEntity>(object key, Func<TEntity> stub, CancellationToken cancellationToken)
        where TEntity : class
    {
        _actions[(typeof(TEntity), key)] = MapAction.Attach;

        return Task.FromResult<TEntity?>(stub());
    }

    public override void Created(object entity, object? key)
    {
        _actions[(entity.GetType(), key)] = MapAction.Create;
    }

    public override void Updated(object entity, object? key)
    {
        _actions[(entity.GetType(), key)] = MapAction.Update;
    }

    public override void Deleted(object entity, object? key)
    {
        _actions[(entity.GetType(), key)] = MapAction.Delete;
    }
}
