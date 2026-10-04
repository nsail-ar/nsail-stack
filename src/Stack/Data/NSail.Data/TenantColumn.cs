// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace NSail.Data;

// The third pass over the whole model, beside row version and collation and for the same
// reason: a rule set once where the model is assembled is a rule no call site can forget.
// Here that matters more than it does there — a forgotten filter is one customer reading
// another's rows.
//
// The model is IDENTICAL in every mode, deliberately. A column and a filter that appeared only
// under SingleDb would be a second model, a second snapshot and a second migration chain,
// authored twice and drifting; instead the column is always there and the filter always
// compares, and it is the VALUE compared against that is constant where no tenant is resolved.
// Every row an install writes under None or MultiDb carries Guid.Empty and the filter asks for
// Guid.Empty, so the predicate matches everything and the mode is the one that shipped before.
static class TenantColumn
{
    public const string Name = "TenantId";

    // Named rather than anonymous, because the org axis sets a filter of its own beside this
    // one and EF refuses to hold an anonymous filter next to a named one. Both apply, and a
    // key opens no way out that was not there before it.
    public const string Key = "Tenant";

    // The install's own rows. Declared as a column default and not only stamped in the tracker,
    // because migrations insert with raw SQL — the seed names its columns by hand and must not
    // have to name this one.
    static readonly Guid Install = Guid.Empty;

    static readonly MethodInfo Property =
        typeof(EF).GetMethod(nameof(EF.Property))!.MakeGenericMethod(typeof(Guid));

    static readonly MethodInfo Resolve =
        typeof(TenantColumn).GetMethod(nameof(Of), BindingFlags.Static | BindingFlags.Public)!;

    public static void Apply(IMutableModel model, ITenanted context)
    {
        foreach (var entity in model.GetEntityTypes().ToArray())
        {
            if (!HasATableOfItsOwn(entity))
            {
                continue;
            }

            if (typeof(IInstallScoped).IsAssignableFrom(entity.ClrType))
            {
                // The install's own reference data: no column, no filter, no stamp — every
                // tenant reads the same rows. Its foreign keys still want the index EF's own
                // convention would have given them, and that convention is gone from this model
                // (ApplyTenancy) so that the tenant-leading ones below are not doubled.
                CoverForeignKeys(entity, lead: null);

                continue;
            }

            var tenant = entity.AddProperty(Name, typeof(Guid));

            tenant.IsNullable = false;
            tenant.ValueGenerated = ValueGenerated.Never;
            tenant.SetDefaultValue(Install);

            entity.SetQueryFilter(Key, Filter(entity.ClrType, context));

            LeadIndexesWithTenant(entity, tenant);
        }
    }

    /// <summary>The tenant every row a context reads and writes belongs to, and null where it
    /// must read and write none. Reached through the context so a Blazor circuit — whose renders
    /// happen long after the flow that resolved the tenant ended — answers the tenant pinned onto
    /// its own scope.</summary>
    public static Guid? Of(ITenanted context)
    {
        return context.TenantId;
    }

    // An owned type has no table of its own here (Branding's colours, an Eye) — its columns are
    // its owner's row, which already carries the tenant. A derived type in a hierarchy shares
    // its root's table and EF refuses a filter anywhere but the root, for the same reason.
    static bool HasATableOfItsOwn(IMutableEntityType entity)
    {
        return !entity.IsOwned() && entity.BaseType is null;
    }

    static LambdaExpression Filter(Type clrType, ITenanted context)
    {
        var row = Expression.Parameter(clrType, "row");

        // The context is captured as a CONSTANT of its own concrete type, which is what lets EF
        // rebind it to the context actually running the query: a filter that closed over the
        // tenant itself would bake the first request's answer into the model for the life of
        // the process.
        var current = Expression.Call(Resolve, Expression.Constant(context));

        // The column is not nullable and the answer is, deliberately: the comparison is lifted so
        // an unresolved scope compares against NULL, which no row equals. That is the whole of
        // "reads nothing" — no branch, no second filter, no mode read at query time.
        return Expression.Lambda(
            Expression.Equal(
                Expression.Convert(Expression.Call(Property, row, Expression.Constant(Name)), typeof(Guid?)),
                current),
            row);
    }

    // A filter is only cheap when the index it rides leads with the tenant. One that does not is
    // scanned whole and filtered after, which is the small tenant paying the large one's size —
    // the tax the mode exists to avoid. Every index the model carries is rebuilt with the tenant
    // in front, foreign keys included: EF's own foreign-key index convention is removed
    // (ModelConfigurationExtensions.ApplyTenancy) precisely so its single-column indexes do not
    // come back beside these.
    static void LeadIndexesWithTenant(IMutableEntityType entity, IMutableProperty tenant)
    {
        foreach (var index in entity.GetIndexes().ToArray())
        {
            if (index.Properties[0] != tenant)
            {
                Rebuild(entity, index, tenant);
            }
        }

        CoverForeignKeys(entity, tenant);
    }

    static void CoverForeignKeys(IMutableEntityType entity, IMutableProperty? lead)
    {
        foreach (var foreignKey in entity.GetForeignKeys().ToArray())
        {
            Cover(entity, foreignKey, lead);
        }
    }

    static void Rebuild(IMutableEntityType entity, IMutableIndex index, IMutableProperty tenant)
    {
        var properties = Led(tenant, index.Properties);
        var descending = index.IsDescending is { } order ? new List<bool> { false }.Concat(order).ToList() : null;
        var annotations = index.GetAnnotations().ToList();
        var name = index.Name;
        var unique = index.IsUnique;

        entity.RemoveIndex(index);

        var replacement = name is null ? entity.AddIndex(properties) : entity.AddIndex(properties, name);

        if (replacement is null)
        {
            return;
        }

        replacement.IsUnique = unique;
        replacement.IsDescending = descending;

        foreach (var annotation in annotations)
        {
            replacement.SetAnnotation(annotation.Name, annotation.Value);
        }
    }

    static void Cover(IMutableEntityType entity, IMutableForeignKey foreignKey, IMutableProperty? lead)
    {
        // The primary key already indexes what leads it, and a Guid key names one row either way.
        if (entity.FindPrimaryKey() is { } key && Leads(key.Properties, foreignKey.Properties))
        {
            return;
        }

        var properties = Led(lead, foreignKey.Properties);

        if (entity.GetIndexes().Any(index => Leads(index.Properties, properties)))
        {
            return;
        }

        if (entity.AddIndex(properties) is { } index)
        {
            index.IsUnique = foreignKey.IsUnique;
        }
    }

    static List<IMutableProperty> Led(IMutableProperty? tenant, IReadOnlyList<IMutableProperty> properties)
    {
        var led = tenant is null ? new List<IMutableProperty>() : new List<IMutableProperty> { tenant };

        led.AddRange(properties);

        return led;
    }

    static bool Leads(IReadOnlyList<IMutableProperty> properties, IReadOnlyList<IMutableProperty> prefix)
    {
        return properties.Count >= prefix.Count
            && prefix.Select((property, position) => properties[position] == property).All(match => match);
    }
}
