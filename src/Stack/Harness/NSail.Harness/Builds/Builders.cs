// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Harness.Builds;

public static class Builders
{
    // The builder a repository names; none when it builds through Commands.
    public static IBuilder? For(BuildOptions? build, int buildNodes)
    {
        return build?.Kind switch
        {
            null => null,
            "DotNet" => new DotNetBuilder(build, buildNodes),
            var kind => throw new InvalidOperationException($"no builder of Kind {kind} (one of: DotNet)"),
        };
    }
}
