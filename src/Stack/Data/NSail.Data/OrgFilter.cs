// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace NSail.Data;

// The fourth pass over the whole model, beside the row version, the collation and the tenant
// column, and for the same reason: a rule set once where the model is assembled is a rule no
// call site can forget. Here it answers the leak a by-id read is — a handler that filters on
// Id alone hands one branch's document to another branch, and there are two hundred of those
// sites, so the cure is a WHERE nobody writes rather than two hundred edits.
//
// It differs from the tenant's pass in the one way the axes differ. The tenant is a wall and
// filters everything; the org is a permission scope INSIDE one and filters only what the map
// classifies as having happened at a branch (org-map.md, carried by IOrgScoped). And it adds no
// column: an entity that records where it happened already says so, and one that rides a head
// belongs where its head belongs — reaching the head's column through the required reference is
// what makes a rider's answer impossible to disagree with its head's.
static class OrgFilter
{
    // The filter carries a key of its own so it stands BESIDE the tenant's rather than
    // replacing it: EF keeps one unnamed filter and any number of named ones, and both apply.
    public const string Key = "Org";

    public const string Column = "OrganizationId";

    static readonly MethodInfo Property =
        typeof(EF).GetMethod(nameof(EF.Property))!.MakeGenericMethod(typeof(Guid));

    static readonly MethodInfo Contains = typeof(Enumerable)
        .GetMethods(BindingFlags.Static | BindingFlags.Public)
        .Single(method => method.Name == nameof(Enumerable.Contains) && method.GetParameters().Length == 2)
        .MakeGenericMethod(typeof(Guid));

    static readonly MethodInfo ResolveVisible =
        typeof(OrgFilter).GetMethod(nameof(Visible), BindingFlags.Static | BindingFlags.Public)!;

    static readonly MethodInfo ResolveEverywhere =
        typeof(OrgFilter).GetMethod(nameof(Everywhere), BindingFlags.Static | BindingFlags.Public)!;

    public static void Apply(IMutableModel model, IHasOrgScope context)
    {
        foreach (var entity in model.GetEntityTypes().ToArray())
        {
            if (!HasATableOfItsOwn(entity) || !IsOrgScoped(entity))
            {
                continue;
            }

            entity.SetQueryFilter(Key, Filter(entity, context));
        }
    }

    /// <summary>The branches the context executing the query can see. Reached through the
    /// context so a Blazor circuit — whose renders happen long after the flow that resolved the
    /// scope ended — answers the scope pinned onto its own.</summary>
    public static Guid[] Visible(IHasOrgScope context)
    {
        return context.OrgScope.Visible;
    }

    /// <summary>Whether the work stands in no branch and therefore reads every one of them —
    /// a job, a first touch, a migration. Read beside the set rather than encoded into it
    /// because "every branch" is not a list of ids: it is the absence of a branch scope.</summary>
    public static bool Everywhere(IHasOrgScope context)
    {
        return context.OrgScope.IsEverywhere;
    }

    static bool IsOrgScoped(IReadOnlyEntityType entity)
    {
        return typeof(IOrgScoped).IsAssignableFrom(entity.ClrType);
    }

    // An owned type has no table of its own here — its columns are its owner's row, which is
    // filtered already — and a derived type shares its root's table, where EF allows the filter
    // only on the root.
    static bool HasATableOfItsOwn(IReadOnlyEntityType entity)
    {
        return !entity.IsOwned() && entity.BaseType is null;
    }

    static LambdaExpression Filter(IMutableEntityType entity, IHasOrgScope context)
    {
        var row = Expression.Parameter(entity.ClrType, "row");
        var axes = Axes(entity, row, [], []);

        if (axes.Count == 0)
        {
            throw new InvalidOperationException(
                $"{entity.DisplayName()} is marked {nameof(IOrgScoped)} and the model gives no way to reach "
                + $"its organization: it declares no {Column} and holds no required reference to an entity "
                + "that does. Either it is not org-scoped after all (org-map.md classifies it, and a row that "
                + "inherits its answer states its parent), or the reference it rides is optional and the "
                + "model does not know it is a rider.");
        }

        // The context is captured as a CONSTANT of its own concrete type, which is what lets EF
        // rebind it to the context actually running the query: a filter that closed over the
        // scope itself would bake the first operation's answer into the model for the life of
        // the process.
        var constant = Expression.Constant(context);

        var visible = axes
            .Select(axis => (Expression)Expression.Call(Contains, Expression.Call(ResolveVisible, constant), axis))
            .Aggregate(Expression.AndAlso);

        // Two reads of the same scope rather than one, because the two states are not one
        // value: a set says WHICH branches, and the flag says there is no branch scope at all.
        // Both are parameters at query time, so the shape of the SQL never varies.
        return Expression.Lambda(
            Expression.OrElse(Expression.Call(ResolveEverywhere, constant), visible),
            row);
    }

    static List<Expression> Axes(
        IReadOnlyEntityType entity,
        Expression row,
        HashSet<IReadOnlyEntityType> visited,
        List<Expression> found)
    {
        if (!visited.Add(entity))
        {
            return found;
        }

        if (entity.FindProperty(Column) is { ClrType: var type } && type == typeof(Guid))
        {
            found.Add(Expression.Call(Property, row, Expression.Constant(Column)));

            return found;
        }

        foreach (var head in Heads(entity))
        {
            Axes(head.PrincipalEntityType, Expression.Property(row, head.DependentToPrincipal!.PropertyInfo!), visited, found);
        }

        return found;
    }

    // A rider rides what it cannot exist without: a REQUIRED reference to an org-scoped entity,
    // reached through a navigation the CLR type declares. An optional one is not a ride — a
    // line that may name no store does not happen where that store stands — and a rider with no
    // navigation of its own is a row the model cannot join, which the filter says out loud
    // rather than silently leaving unfiltered.
    static IEnumerable<IReadOnlyForeignKey> Heads(IReadOnlyEntityType entity)
    {
        return entity.GetForeignKeys()
            .Where(foreignKey => foreignKey.IsRequired)
            .Where(foreignKey => foreignKey.DependentToPrincipal?.PropertyInfo is not null)
            .Where(foreignKey => IsOrgScoped(foreignKey.PrincipalEntityType))
            .Where(foreignKey => HasATableOfItsOwn(foreignKey.PrincipalEntityType));
    }
}
