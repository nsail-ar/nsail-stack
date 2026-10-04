// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Data;

/// <summary>Which branches this flow of work can see, and the one service the org filter reads.
/// The default answers <see cref="OrgScope.Everywhere"/>, so a host that resolves no scope —
/// a job, a fixture, a migration — behaves exactly as it did before the filter existed; whoever
/// knows what branch the caller stands in enters the subtree instead (Iam, once per operation).
///
/// <para>An entered scope travels with the async flow as well as with the instance, the same
/// pair <see cref="AmbientTenant"/> and the session provider already keep, and for the same
/// reason: an operation opens service scopes of its own and a context resolved outside DI —
/// a design-time factory's, a fixture's — holds <see cref="Ambient"/> and would otherwise never
/// hear the answer. The instance is what a Blazor circuit keeps, whose renders happen long after
/// the flow that resolved the scope ended.</para></summary>
public class OrgScopeProvider
{
    static readonly AsyncLocal<OrgScope?> Flowing = new();

    /// <summary>The instance a context built outside DI reads through — it owns no scope of its
    /// own and answers whatever the flow it is queried in entered.</summary>
    public static OrgScopeProvider Ambient { get; } = new();

    OrgScope? _entered;

    public virtual OrgScope Current
    {
        get { return _entered ?? Flowing.Value ?? OrgScope.Everywhere; }
    }

    /// <summary>Whether this flow has already answered. The walk down the chart is one query;
    /// an operation that nests sends asks once and every send inside it reads that answer.</summary>
    public bool IsEntered
    {
        get { return _entered is not null || Flowing.Value is not null; }
    }

    public void Enter(OrgScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);

        _entered = scope;
        Flowing.Value = scope;
    }

    /// <summary>Lifts the org filter for one query and restores the operation's own scope once it
    /// answers — the guard that must NOT find a row fails open when the filter hides one in
    /// another branch (org-map.md, G2, data.md — The org filter). Nothing after the query reads
    /// wider than the caller was let ask about.</summary>
    public async Task<T> ReadEverywhere<T>(Func<Task<T>> query)
    {
        ArgumentNullException.ThrowIfNull(query);

        var standing = Current;

        Enter(OrgScope.Everywhere);

        try
        {
            return await query().ConfigureAwait(false);
        }
        finally
        {
            Enter(standing);
        }
    }
}
