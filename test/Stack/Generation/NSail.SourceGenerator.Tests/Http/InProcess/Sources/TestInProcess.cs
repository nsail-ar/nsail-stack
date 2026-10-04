// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.SourceGeneration.Annotations;
using Microsoft.Extensions.DependencyInjection;

namespace NSail.SourceGenerator.Tests.Transport.HttpInProcess;

public partial class TestInProcess
{
    [Generated(NSail.SourceGeneration.Annotations.Http.InProcess)]
    [Source(Assembly="NSail.SourceGenerator.TestAssets")]
    public partial void AddTestInProcess(this IServiceCollection services);
}
