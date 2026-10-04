// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Globalization;
using NSail.Metadata;

namespace NSail.Problems;

public static class BusinessProblem
{
    // The entity slot travels as the concept's KEY, and the key is derived here because this is
    // the last place that still has the type: a refusal is read by a browser that carries the
    // catalogs and none of the .Data assemblies, so nothing downstream can turn Store back into
    // a Type. The provider is constructed rather than injected — a static factory has no
    // container, the same seam TelemetryOptions.ServiceFor already stands on — which costs a
    // company's MetadataProvider override on this one derivation and buys the entity name in
    // the caller's language on every refusal in the tree.
    static readonly MetadataProvider _metadata = new();

    // The business codes a client reads back off the wire BY NAME are constants instead of the
    // same literal written on both sides — NsPartial turns InUse into the delete refusal the
    // user reads, and a screen acting after its own save (a contributed appointment section
    // whose kit has nothing to move) tells NotFound apart from every other refusal. The rest
    // stay literals because nobody matches on them.
    public const string InUseCode = "InUse";

    public const string NotFoundCode = "NotFound";

    public const string ConflictCode = "Conflict";

    /// <summary>A rule that anchors to nothing the screen renders: the rule names itself and
    /// its text lives at "Problems.RuleViolation.{rule}". The arguments fill the named tokens
    /// of that text, so a rule whose English fallback interpolates a runtime detail keeps the
    /// detail once it is translated instead of trading it for a generic sentence.</summary>
    public static Problem RuleViolation(string rule, string message, IReadOnlyDictionary<string, string>? arguments = null)
    {
        return new(
            code: "RuleViolation",
            title: "The operation could not be completed",
            issues: new[]
            {
                new Issue(
                    code: "RuleViolation",
                    message: message,
                    source: rule,
                    arguments: arguments)
            },
            status: 422);
    }

    /// <summary>A rule that anchors to a FIELD — the same split the Invalid family already
    /// makes (InputProblemBuilder.AddInvalid(code, field, message)): the rule is the key
    /// ("Problems.{rule}"), the field is only where the refusal is shown. One field carries as
    /// many rules as it has, which the two-argument overload cannot express because there the
    /// single argument is both.</summary>
    public static Problem RuleViolation(string rule, string field, string message)
    {
        return new(
            code: "RuleViolation",
            title: "The operation could not be completed",
            issues: new[]
            {
                new Issue(
                    code: rule,
                    message: message,
                    source: field)
            },
            status: 422);
    }

    public static Problem InvalidState(string state)
    {
        return new(
            code: "InvalidState",
            title: "Invalid state",
            issues: new[]
            {
                new Issue(
                    code: "InvalidState",
                    message: $"Invalid state: {state}",
                    source: state,
                    arguments: new Dictionary<string, string> { ["state"] = state })
            },
            status: 422);
    }

    public static Problem NotFound(Type entity, object id, string? source = null)
    {
        return NotFound(KeyOf(entity), id, source);
    }

    /// <summary>The entity as its concept KEY ("Products.Store"), for the caller that cannot
    /// name the type: one kit refusing on another's row, or a failure with no entity behind it
    /// at all. Every caller that can reach the type passes it instead.</summary>
    public static Problem NotFound(string entityKey, object id, string? source = null)
    {
        var name = NameOf(entityKey);

        return new(
            code: NotFoundCode,
            title: "Resource not found",
            issues: new[]
            {
                new Issue(
                    code: NotFoundCode,
                    message: $"{name} with id {id} was not found",
                    source: source ?? name,
                    arguments: new Dictionary<string, string>
                    {
                        ["entity"] = entityKey,
                        ["id"] = id?.ToString() ?? string.Empty
                    })
            },
            status: 404);
    }

    // The version token is what refuses the save; the row's own audit timestamp only narrates
    // it. That is why the timestamp rides as a second ISSUE code under the same Problem code:
    // a client matching the refusal keeps reading Conflict whether or not the entity carried a
    // timestamp to tell the user when it changed under them.
    public static Problem Conflict(Type entity, DateTime? modifiedAt = null)
    {
        return Conflict(KeyOf(entity), modifiedAt);
    }

    /// <summary>The entity as its concept KEY, for the caller that cannot name the type.</summary>
    public static Problem Conflict(string entityKey, DateTime? modifiedAt = null)
    {
        var name = NameOf(entityKey);
        var arguments = new Dictionary<string, string> { ["entity"] = entityKey };

        if (modifiedAt is { } instant)
        {
            // Stored UTC, read on somebody's wall clock: the host's local zone is the one the
            // rest of the model already treats as the user's (data.md, BusinessDate.Today).
            arguments["at"] = instant.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);
        }

        return new(
            code: ConflictCode,
            title: "Concurrency conflict",
            issues: new[]
            {
                new Issue(
                    code: modifiedAt is null ? ConflictCode : "ConflictAt",
                    message: modifiedAt is null
                        ? $"The resource {name} was modified by another process"
                        : $"The resource {name} was modified by another process at {arguments["at"]}",
                    source: name,
                    arguments: arguments)
            },
            status: 409);
    }

    public static Problem AlreadyExists(Type entity, object value)
    {
        return AlreadyExists(KeyOf(entity), value);
    }

    /// <summary>The entity as its concept KEY, for the caller that cannot name the type.</summary>
    public static Problem AlreadyExists(string entityKey, object value)
    {
        var name = NameOf(entityKey);

        return new(
            code: "AlreadyExists",
            title: "Resource already exists",
            issues: new[]
            {
                new Issue(
                    code: "AlreadyExists",
                    message: $"{name} with value {value} already exists",
                    source: name,
                    arguments: new Dictionary<string, string>
                    {
                        ["entity"] = entityKey,
                        ["value"] = value?.ToString() ?? string.Empty
                    })
            },
            status: 409);
    }

    // A delete refused because something still points at the row. 409 like AlreadyExists: both
    // are "the state of the resource is what stops you", not a malformed request.
    public static Problem InUse(Type entity)
    {
        return InUse(KeyOf(entity));
    }

    /// <summary>The entity as its concept KEY, for the caller that cannot name the type.</summary>
    public static Problem InUse(string entityKey)
    {
        var name = NameOf(entityKey);

        return new(
            code: InUseCode,
            title: "Resource in use",
            issues: new[]
            {
                new Issue(
                    code: InUseCode,
                    message: $"{name} is referenced by other records and cannot be deleted",
                    source: name,
                    arguments: new Dictionary<string, string> { ["entity"] = entityKey })
            },
            status: 409);
    }

    // Named for the line's own description, never the rate id: a caller who billed a product
    // with no alícuota assigned reads "TaxRate 00000000-... was not found" as pure internals
    // (messaging.md's Errors rule) — the honest refusal names the article that needs a rate,
    // the same value the line already carries whether it came from a product or free text.
    public static Problem MissingTaxRate(string product)
    {
        return new(
            code: "MissingTaxRate",
            title: "Missing tax rate",
            issues: new[]
            {
                new Issue(
                    code: "MissingTaxRate",
                    message: $"{product} has no tax rate assigned",
                    source: "Product",
                    arguments: new Dictionary<string, string> { ["product"] = product })
            },
            status: 422);
    }

    // The alícuota refusal's sibling: a selling price is resolved from a price list's rules over
    // the supplier's cost, so a product no rule reaches — or one whose rule prices over a cost
    // nobody quotes — has no price at all. Zero is not that answer: zero is a price, and an
    // article nobody priced is not free, so the refusal names the article the way MissingTaxRate
    // does.
    public static Problem MissingPrice(string product)
    {
        return new(
            code: "MissingPrice",
            title: "Missing price",
            issues: new[]
            {
                new Issue(
                    code: "MissingPrice",
                    message: $"{product} has no price the selling list can resolve",
                    source: "Product",
                    arguments: new Dictionary<string, string> { ["product"] = product })
            },
            status: 422);
    }

    // Named for the article, not the line: two lines of the same product would collide on
    // "Product" as a field source, so the refusal names what the caller supplied (messaging.md's
    // Errors rule) instead of anchoring to a member no form renders.
    public static Problem InsufficientStock(string product, decimal available)
    {
        var quantity = available.ToString("0.####");

        return new(
            code: "InsufficientStock",
            title: "Insufficient stock",
            issues: new[]
            {
                new Issue(
                    code: "InsufficientStock",
                    message: $"Insufficient stock of {product}: {quantity} available",
                    source: "Product",
                    arguments: new Dictionary<string, string>
                    {
                        ["product"] = product,
                        ["available"] = quantity
                    })
            },
            status: 422);
    }

    static string KeyOf(Type entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return _metadata.KeyFor(entity);
    }

    // What a reader with no catalog is left with. The English message and Issue.Source keep the
    // bare name so an untranslated refusal reads "Store was not found" and a scoped
    // "Problems.{Code}.{Source}" key stays the short one a person writes.
    static string NameOf(string entityKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityKey);

        return entityKey[(entityKey.LastIndexOf('.') + 1)..];
    }
}
