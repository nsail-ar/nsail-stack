// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.Data;

/// <summary>The ids of rows an install freezes and code names as constants — a seeded Role, the
/// account a setting defaults to. Declared beside the constants themselves, because the constants
/// ARE the set: there is no registry to keep in step and nothing is discovered.</summary>
public interface IWellKnownRows
{
    IEnumerable<Guid> Ids { get; }
}

/// <summary>The closed set <see cref="TenancyProvider.Row(Guid)"/> translates, and the reason the
/// translation is safe to apply to a value that may not be frozen at all: a stored setting holds
/// either the install's frozen id or a row the tenant chose itself, and only the first is in here.
/// An id outside the set is somebody's actual row and passes through untouched.</summary>
public sealed class WellKnownRows
{
    /// <summary>An install that declared none — every id passes through, which is what a mode
    /// whose rows are not a tenant's does anyway.</summary>
    public static WellKnownRows None { get; } = new([]);

    readonly HashSet<Guid> _ids;

    public WellKnownRows(IEnumerable<IWellKnownRows> declarations)
    {
        ArgumentNullException.ThrowIfNull(declarations);

        _ids = [.. declarations.SelectMany(declaration => declaration.Ids)];
    }

    public bool Contains(Guid id)
    {
        return _ids.Contains(id);
    }
}

/// <summary>The ids the seeded rows of an <see cref="IInstallScoped"/> entity carry — a tax rate, a
/// taxpayer category, a kind of address. Declared beside those rows, exactly as the well-known set
/// is, and read for the opposite reason: there is ONE of each row and it is everybody's.</summary>
public interface IInstallScopedRows
{
    IEnumerable<Guid> Ids { get; }
}

/// <summary>The ids no scope owns a copy of, and therefore the only ids a document may name
/// verbatim (<see cref="TenancyProvider.Owned(Guid)"/>). Everything else a document names is a row
/// of the scope importing it, so the set decides which of a document's guids survive it.</summary>
public sealed class InstallScopedRows
{
    /// <summary>An install that declared none — a document names no shared row, so every id in it
    /// is the importing scope's own.</summary>
    public static InstallScopedRows None { get; } = new([]);

    readonly HashSet<Guid> _ids;

    public InstallScopedRows(IEnumerable<IInstallScopedRows> declarations)
    {
        ArgumentNullException.ThrowIfNull(declarations);

        _ids = [.. declarations.SelectMany(declaration => declaration.Ids)];
    }

    public bool Contains(Guid id)
    {
        return _ids.Contains(id);
    }
}

sealed class DeclaredRows : IWellKnownRows, IInstallScopedRows
{
    public DeclaredRows(IEnumerable<Guid> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);

        Ids = [.. ids];
    }

    public IEnumerable<Guid> Ids { get; }
}
