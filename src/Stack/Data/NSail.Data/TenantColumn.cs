// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

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
        var owned = new Dictionary<IMutableEntityType, IMutableProperty>();

        foreach (var entity in model.GetEntityTypes().ToArray())
        {
            if (!HasATableOfItsOwn(entity))
            {
                RefuseASecondTable(entity);

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

            // Generated on add from the context, so a new row's column is known before any stamp:
            // EF refuses to insert a row whose shadow key it does not know, and the column sits in
            // a key wherever another table names the row. A context AddDataAccess did not build —
            // a design-time factory's, a retrofit's — has no stamp to make it known.
            tenant.IsNullable = false;
            tenant.ValueGenerated = ValueGenerated.OnAdd;
            tenant.SetValueGeneratorFactory((_, _) => new TenantKeyGenerator());
            tenant.SetDefaultValue(Install);

            entity.SetQueryFilter(Key, Filter(entity.ClrType, context));

            owned.Add(entity, tenant);
        }

        // After every column exists: a foreign key names the principal's column as well as its
        // own, and the principal may come later in the model.
        foreach (var (entity, tenant) in owned)
        {
            ScopeKeys(entity, tenant);
        }

        // Every type but the owned ones, not only the roots: a derived type shares its root's row
        // and may declare an index or a foreign key of its own, which names the same tenant. An
        // owned type cannot name its owner's column in a key or an index of its own, so what it
        // declares stays plain.
        var sharing = model.GetEntityTypes().Where(entity => !entity.IsOwned()).ToArray();

        foreach (var entity in sharing)
        {
            if (TenantOf(entity, owned) is { } tenant)
            {
                LeadIndexesWithTenant(entity, tenant);
            }
        }

        foreach (var entity in sharing)
        {
            ScopeForeignKeys(entity, owned);
        }

        foreach (var (entity, tenant) in owned)
        {
            DropGlobalKeys(entity, tenant);
        }

        foreach (var entity in sharing)
        {
            if (TenantOf(entity, owned) is { } tenant)
            {
                CoverForeignKeys(entity, tenant);
            }
        }
    }

    // The tenant column of the row a type shares or is: its root's — the base of a hierarchy, the
    // owner of an aggregate — and only where the root took one. Asked of the marker's outcome and
    // not of the column, because TenantState carries a TenantId of its own that is not the wall's.
    static IMutableProperty? TenantOf(
        IMutableEntityType entity,
        Dictionary<IMutableEntityType, IMutableProperty> owned)
    {
        var root = entity;

        for (var above = Above(root); above is not null; above = Above(root))
        {
            root = above;
        }

        return owned.TryGetValue(root, out var tenant) ? tenant : null;
    }

    // An alternate key is unique per tenant, like every unique index: the tenant leads it, so two
    // tenants hold the same value and one never learns, from a unique violation, that another
    // holds it. The primary key stays Id alone. The global key goes once the foreign keys that
    // named it name the tenant's (ScopeForeignKeys).
    static void ScopeKeys(IMutableEntityType entity, IMutableProperty tenant)
    {
        foreach (var key in entity.GetDeclaredKeys().ToArray())
        {
            if (key == entity.FindPrimaryKey() || key.Properties[0] == tenant)
            {
                continue;
            }

            var led = entity.FindKey(Led(tenant, key.Properties)) ?? entity.AddKey(Led(tenant, key.Properties));

            foreach (var annotation in key.GetAnnotations())
            {
                led.SetAnnotation(annotation.Name, annotation.Value);
            }
        }
    }

    static void DropGlobalKeys(IMutableEntityType entity, IMutableProperty tenant)
    {
        foreach (var key in entity.GetDeclaredKeys().ToArray())
        {
            if (key != entity.FindPrimaryKey()
                && key.Properties[0] != tenant
                && !key.GetReferencingForeignKeys().Any())
            {
                entity.RemoveKey(key);
            }
        }
    }

    // A foreign key between two tenant entities is (TenantId, Fk) onto (TenantId, Key), so the
    // database refuses a row naming another tenant's: referential checks bypass the row policy,
    // and a constraint that does not carry the tenant would accept any Guid that exists. A key
    // onto an install-scoped entity stays plain — it is the same row for every tenant.
    static void ScopeForeignKeys(IMutableEntityType entity, Dictionary<IMutableEntityType, IMutableProperty> owned)
    {
        foreach (var foreignKey in entity.GetDeclaredForeignKeys().ToArray())
        {
            var dependent = TenantOf(entity, owned);
            var principal = TenantOf(foreignKey.PrincipalEntityType, owned);

            if (dependent is null
                || principal is null
                || foreignKey.IsOwnership
                || foreignKey.Properties[0] == dependent)
            {
                continue;
            }

            var target = foreignKey.PrincipalKey;
            var declaring = target.DeclaringEntityType;

            if (!owned.ContainsKey(declaring))
            {
                continue;
            }

            var scoped = target.Properties[0] == principal
                ? target
                : declaring.FindKey(Led(principal, target.Properties)) ?? declaring.AddKey(Led(principal, target.Properties));

            foreignKey.SetProperties(Led(dependent, foreignKey.Properties), scoped);

            // ON DELETE SET NULL on a composite key nulls every column of it, the tenant's
            // included, which the column refuses; the context clears the key instead.
            if (foreignKey.DeleteBehavior == DeleteBehavior.SetNull)
            {
                foreignKey.DeleteBehavior = DeleteBehavior.ClientSetNull;
            }
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

    // And where that premise is false, the model is refused. A type the pass above skips because
    // it shares somebody's row, mapped to a table of its OWN — an owned navigation told ToTable,
    // an owned collection, a derived type given its own table — is a second table holding every
    // tenant's rows behind neither wall, and neither wall can be hung on it: EF refuses a query
    // filter anywhere but the root, and the policy the chain would write needs a column that is
    // not there. The sweep cannot catch it either — it reads the model for tables that owe a
    // policy, and this one owes none — so the refusal is here, where the shape is visible.
    //
    // Unless the row it shares is the INSTALL's. A table hanging off an install-scoped root
    // holds the same rows for every tenant, which is what the marker means and not a defect:
    // the remedy this refusal offers — an entity of its own — would give exactly the table that
    // is already there, with no column, no filter and no policy, because the pass skips an
    // install-scoped entity by the marker. The marker is asked, never the column (TenantStamp).
    static void RefuseASecondTable(IMutableEntityType entity)
    {
        var shared = Above(entity);

        if (shared is null || entity.GetTableName() is not { } table || table == shared.GetTableName())
        {
            return;
        }

        if (SharesTheInstallsOwnRows(entity))
        {
            return;
        }

        throw new InvalidOperationException(
            $"{entity.DisplayName()} shares {shared.DisplayName()}'s row — it carries no tenant column, no query filter and no row policy — and is mapped to a table of its own, \"{table}\", which would hold every tenant's rows behind neither wall. Map it to its owner's table, or make it an entity of its own.");
    }

    // Asked of the ROOT of the chain the type hangs off, not of the type beside it: an owned
    // collection two levels under an install-scoped entity reaches it through owned types that
    // carry no marker of their own, and a TPT leaf reaches it through its base.
    static bool SharesTheInstallsOwnRows(IMutableEntityType entity)
    {
        var root = entity;

        for (var above = Above(root); above is not null; above = Above(root))
        {
            root = above;
        }

        return typeof(IInstallScoped).IsAssignableFrom(root.ClrType);
    }

    // Whose row a type shares: its base's in a hierarchy, its owner's in an aggregate.
    static IMutableEntityType? Above(IMutableEntityType entity)
    {
        return entity.BaseType ?? entity.FindOwnership()?.PrincipalEntityType;
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
        foreach (var index in entity.GetDeclaredIndexes().ToArray())
        {
            if (index.Properties[0] != tenant)
            {
                Rebuild(entity, index, tenant);
            }
        }
    }

    static void CoverForeignKeys(IMutableEntityType entity, IMutableProperty? lead)
    {
        foreach (var foreignKey in entity.GetDeclaredForeignKeys().ToArray())
        {
            Cover(entity, foreignKey, foreignKey.Properties[0] == lead ? null : lead);
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
        // The primary key already indexes what leads it, and a Guid key names one row either way —
        // the tenant in front of a foreign key that is the key itself included.
        if (entity.FindPrimaryKey() is { } key
            && (Leads(key.Properties, foreignKey.Properties)
                || (lead is null && foreignKey.Properties.Count > 1 && foreignKey.Properties[0].Name == Name && Leads(key.Properties, foreignKey.Properties.Skip(1).ToArray()))))
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
