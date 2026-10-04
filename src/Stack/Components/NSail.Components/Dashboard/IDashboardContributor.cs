// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components;

/// <summary>Contributes a module's cards to the dashboard. Async so a contributor may vary
/// with the session — permissions are not its job, the gate is.</summary>
public interface IDashboardContributor
{
    Task<IReadOnlyList<DashboardItem>> GetItems();
}

/// <summary>Contributes a module's cards to the dashboard of one subject — the same
/// mechanism scoped to a row the cards are about, so a person's page is a dashboard of that
/// person rather than a fourth contribution pattern. TSubject names which dashboard: a host
/// resolves only the contributors closed over the subject it carries.</summary>
public interface IDashboardContributor<TSubject>
{
    Task<IReadOnlyList<DashboardItem>> GetItems(TSubject subject);
}
