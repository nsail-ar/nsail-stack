// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Harness.Planning;

// What one machine may run at once, per resource. A Dev lane holds an Agent slot for
// its whole voyage and takes a Build slot only while it compiles (the build shim waits for
// one), so builds are rationed where they actually happen.
public sealed record Limits(int Agents, int Builds, int Browsers)
{
    public int Of(Resource r)
    {
        return r switch
        {
            Resource.Agent => Agents,
            Resource.Build => Builds,
            Resource.Browser => Browsers,
            _ => 0,
        };
    }
}
