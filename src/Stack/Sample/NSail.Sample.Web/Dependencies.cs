// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.DependencyInjection;
using NSail.SourceGeneration.Annotations;

namespace NSail.Sample;

internal static partial class Dependencies
{
    [Generated(Services.Registration)]
    internal static partial void AddSampleServices(this IServiceCollection services);
}
