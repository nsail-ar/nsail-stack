// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using System.Text.Json;
using System.Text.Json.Serialization;

namespace NSail.Security;

/// <summary>The one way a policy is written to and read from storage. Both sides share this
/// options instance on purpose: a seed that writes camelCase and a loader that reads with
/// the defaults would produce a row that deserializes into an empty document and a grant
/// that quietly does nothing, which is the worst failure a permission can have.</summary>
public static class PolicyDocument
{
    static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        // The constraint vocabulary is a closed set of names. Storing the integers would
        // make an inserted row unreadable to a human and would renumber itself the day the
        // enum grows a member in the middle.
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,

        // A key outside the schema is never harmless here. Skipped silently, a mistyped
        // "fields" leaves a policy with no constraints at all — the full-capability shape,
        // handed to whoever the audience names — and a key an author believed in reads as
        // accepted while meaning nothing. Every stored document is produced by Write below,
        // so nothing legitimate carries a key this does not know.
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    public static string Write(Policy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        return JsonSerializer.Serialize(policy, Options);
    }

    public static Policy Read(string document)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(document);

        return JsonSerializer.Deserialize<Policy>(document, Options)
            ?? throw new InvalidOperationException("A stored policy deserialized to nothing.");
    }

    /// <summary>The stored value, read and written back through today's <see cref="Policy"/>
    /// shape. An exact-document guard compares against this rather than the raw column: a row
    /// an older assembly planted never carries a property this one later added (a bool defaults
    /// the same way whether the author never touched it or the type didn't have it yet), so
    /// comparing the raw bytes against what today's builder produces would call every such row
    /// edited and never reach it. Reading it back first answers the same question — "is this
    /// still the shape we shipped?" — without depending on which schema wrote it.</summary>
    public static string Reserialize(string document)
    {
        return Write(Read(document));
    }

    /// <summary>A stored document with one constraint merged in, for a seed step that has to
    /// reach a row it already planted. A field the row already answers about is left exactly as
    /// it is: the shop or practice that narrowed, widened or renamed its own copy made a
    /// decision, and the only thing a retrofit is entitled to change is the field nobody chose.
    /// The document is returned unchanged when there is nothing to add, so the caller's
    /// comparison against the old value is what decides whether a row was touched at all.</summary>
    public static string WithConstraint(string document, string field, PolicyConstraint constraint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(field);
        ArgumentNullException.ThrowIfNull(constraint);

        var policy = Read(document);

        if (policy.Fields?.ContainsKey(field) == true)
        {
            return document;
        }

        var fields = policy.Fields is null
            ? new Dictionary<string, PolicyConstraint>(StringComparer.Ordinal)
            : new Dictionary<string, PolicyConstraint>(policy.Fields, StringComparer.Ordinal);

        fields[field] = constraint;

        return Write(new Policy
        {
            Id = policy.Id,
            Name = policy.Name,
            Messages = policy.Messages,
            Audience = policy.Audience,
            Fields = fields,
            EveryOrganization = policy.EveryOrganization,
            ValidFrom = policy.ValidFrom,
            ValidTo = policy.ValidTo,
        });
    }

    /// <summary>A stored document with one message merged into its list, for a seed step that has
    /// to reach a row the pack widened after a shop's own import already planted it (a pack row is
    /// planted once, by id — <c>CreatePolicy</c> on the frozen id answers <c>AlreadyExists</c> ever
    /// after). A list already carrying the exact message — the one the step is told to merge,
    /// which may itself be a wildcard — is left exactly as it is: a shop that narrowed its copy
    /// made a decision this retrofit has no business widening back. The document is returned
    /// unchanged when there is nothing to add, so the caller's comparison against the old value is
    /// what decides whether a row was touched at all.</summary>
    public static string WithMessage(string document, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        var policy = Read(document);

        if (policy.Messages.Contains(message, StringComparer.Ordinal))
        {
            return document;
        }

        return Write(new Policy
        {
            Id = policy.Id,
            Name = policy.Name,
            Messages = [.. policy.Messages, message],
            Audience = policy.Audience,
            Fields = policy.Fields,
            EveryOrganization = policy.EveryOrganization,
            ValidFrom = policy.ValidFrom,
            ValidTo = policy.ValidTo,
        });
    }
}
