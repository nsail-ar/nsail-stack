// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Components;

namespace NSail.Storage;

public sealed class Guide : IGuideContributor
{
    public Task<IReadOnlyList<GuideItem>> GetItems()
    {
        return Task.FromResult<IReadOnlyList<GuideItem>>(
        [
            new GuideItem
            {
                Name = "Storage",
                File = "storage",
                Weight = 87
            }
        ]);
    }
}
