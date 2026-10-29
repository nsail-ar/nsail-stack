// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Security;

/// <summary>Contributes the entitlement keys a module owns — the shape of
/// <c>ISettingsContributor</c>, and the same reason: the Stack holds no list of keys, because a
/// key belongs beside the feature that enforces it and nothing central can be kept honest.</summary>
public interface IFeatureContributor
{
    IReadOnlyList<FeatureItem> GetItems();
}
