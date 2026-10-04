// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components;

/// <summary>Contributes a module's chapters to the app's guide. Registering the contributor
/// IS the contribution — a module an app never composed registers nothing, so its chapters
/// cannot reach that app's guide at all. The list costs no I/O: it names files, and a
/// chapter's own prose is fetched when a reader opens it.</summary>
public interface IGuideContributor
{
    Task<IReadOnlyList<GuideItem>> GetItems();
}
