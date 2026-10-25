// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Security;

/// <summary>Which of the two things a key in the entitlements bag is. They are not
/// interchangeable and nothing may swap them: a flag is hard — what it names is not offered and
/// is refused at the gate — and a ceiling is soft, stated and never blocking.</summary>
public enum FeatureKind
{
    Flag,
    Limit,
}

/// <summary>One key of the entitlements bag, as the module that enforces it declares it: what it
/// is called, which of the two kinds it is, and what it answers for a tenant nobody has written
/// it for. The Stack gathers these and names none of them — whoever sells the feature declares
/// the key beside the code that enforces it.</summary>
public sealed class FeatureItem
{
    public required string Key { get; init; }

    public required FeatureKind Kind { get; init; }

    /// <summary>What a <see cref="FeatureKind.Flag"/> answers where the bag is silent. Declared
    /// and not assumed, and <b>permissive is the only safe value</b> for a key that ships before
    /// anybody has been sold it: the fleet's bags are empty, so a flag defaulting to off would
    /// cut paying customers off on deploy day.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>What a <see cref="FeatureKind.Limit"/> answers where the bag is silent, or null
    /// for no ceiling at all — which is what an unsold limit has to be, for the reason
    /// above.</summary>
    public decimal? Limit { get; init; }
}

/// <summary>Contributes the entitlement keys a module owns — the shape of
/// <c>ISettingsContributor</c>, and the same reason: the Stack holds no list of keys, because a
/// key belongs beside the feature that enforces it and nothing central can be kept honest.</summary>
public interface IFeatureContributor
{
    IReadOnlyList<FeatureItem> GetItems();
}
