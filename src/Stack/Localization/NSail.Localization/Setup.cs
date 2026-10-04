// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSail.Metadata;

namespace NSail.Localization;

public static class Setup
{
    public static void AddLocalization(this IServiceCollection services)
    {
        services.AddMetadata();
        services.TryAddSingleton<StringCatalog>();
        services.TryAddScoped<LanguageProvider>();
        services.TryAddScoped<StringManager>();
    }

    // NoInlining keeps GetCallingAssembly honest: inlined, it would report this assembly.
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void AddStringsFromAssembly(this IServiceCollection services, Assembly? assembly = null)
    {
        assembly ??= Assembly.GetCallingAssembly();

        services.AddLocalization();
        services.AddSingleton<IStringSource>(new EmbeddedStrings(assembly));
    }
}
