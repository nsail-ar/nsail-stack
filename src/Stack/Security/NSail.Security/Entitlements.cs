// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Text.Json;

namespace NSail.Security;

/// <summary>What the install records that a tenant is entitled to: one key against a flag or a
/// number, as it was written into the bag beside the tenant's plan. The Stack carries it without
/// naming a key, exactly as it carries the plan's word without naming a plan — whoever sells the
/// features declares what the keys are, which kind each one is and what each one defaults to.
///
/// <para><b>Every answer here is the permissive one</b> (<see cref="Unrestricted"/>): a bag
/// nobody has written, a key nobody has written in it, a value of the wrong kind and text no
/// parser can read all answer "enabled" and "no ceiling". The bag's writer is a program outside
/// this codebase, so a typo in its hand must not take a paying customer down — the same rule
/// the rest of the row is read by (data-tenancy.md, The install's own record of a tenant). What
/// a key's own default is belongs to whoever declared the key: this reads the bag and nothing
/// else.</para></summary>
public sealed class Entitlements
{
    /// <summary>The bag of an install nobody has written one for: every feature on and no
    /// ceiling in force, which is what every tenant is until something is sold to it.</summary>
    public static Entitlements Unrestricted { get; } = new(new Dictionary<string, bool>(), new Dictionary<string, decimal>());

    readonly Dictionary<string, bool> _flags;
    readonly Dictionary<string, decimal> _limits;

    Entitlements(Dictionary<string, bool> flags, Dictionary<string, decimal> limits)
    {
        _flags = flags;
        _limits = limits;
    }

    /// <summary>The bag the column holds, or <see cref="Unrestricted"/> for anything this cannot
    /// read: no text, text that is not a JSON object, and — per key — a value that is neither a
    /// flag nor a number. Nothing here throws, and that is the point.</summary>
    public static Entitlements Read(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Unrestricted;
        }

        var flags = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        var limits = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var document = JsonDocument.Parse(json);

            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return Unrestricted;
            }

            foreach (var member in document.RootElement.EnumerateObject())
            {
                switch (member.Value.ValueKind)
                {
                    case JsonValueKind.True:
                    case JsonValueKind.False:
                        flags[member.Name] = member.Value.GetBoolean();

                        break;

                    case JsonValueKind.Number when member.Value.TryGetDecimal(out var number):
                        limits[member.Name] = number;

                        break;
                }
            }
        }
        catch (JsonException)
        {
            return Unrestricted;
        }

        return new Entitlements(flags, limits);
    }

    /// <summary>Whether the feature is open for this tenant: false only where the bag says so in
    /// as many words. Absent, of the wrong kind, or no bag at all — all on.</summary>
    public bool IsEnabled(string feature)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(feature);

        return !_flags.TryGetValue(feature, out var enabled) || enabled;
    }

    /// <summary>The ceiling written for this key, or null for no ceiling — which is what an
    /// absent key, a key written as a flag and an unreadable bag all answer. <b>A ceiling
    /// never blocks the work</b>: what reads this states how much of it is used and carries on
    /// (the story that declares the key).</summary>
    public decimal? Limit(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return _limits.TryGetValue(key, out var limit) ? limit : null;
    }

    /// <summary>Every key the bag carries, of either kind — what a screen that shows an operator
    /// what was written reads, and what a client is handed beside its effective policies.</summary>
    public IReadOnlyCollection<string> Keys
    {
        get { return _flags.Keys.Concat(_limits.Keys).ToList(); }
    }

    /// <summary>The flags the bag states, for whoever has to carry them across a wire.</summary>
    public IReadOnlyDictionary<string, bool> Flags
    {
        get { return _flags; }
    }

    /// <summary>The ceilings the bag states, beside the flags above.</summary>
    public IReadOnlyDictionary<string, decimal> Limits
    {
        get { return _limits; }
    }

    /// <summary>The bag a caller already holds as values rather than as text — the client half,
    /// which is handed what the server read instead of reading the column itself.</summary>
    public static Entitlements Of(
        IReadOnlyDictionary<string, bool>? flags,
        IReadOnlyDictionary<string, decimal>? limits)
    {
        if (flags is not { Count: > 0 } && limits is not { Count: > 0 })
        {
            return Unrestricted;
        }

        return new Entitlements(
            new Dictionary<string, bool>(flags ?? new Dictionary<string, bool>(), StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, decimal>(limits ?? new Dictionary<string, decimal>(), StringComparer.OrdinalIgnoreCase));
    }
}
