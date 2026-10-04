// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace NSail.Metadata;

public static class Setup
{
    /// <summary>TryAdds the provider — every consumer declares it, order never matters:
    /// the template lives in each assembly's MetadataTemplate attribute, not here. A
    /// custom policy (MetadataProvider subclass) registers before the TryAdds.</summary>
    public static void AddMetadata(this IServiceCollection services)
    {
        services.TryAddSingleton<MetadataProvider>();
    }
}
