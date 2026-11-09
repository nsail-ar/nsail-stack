// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Harness.Tests;

// A fact that runs only on one platform and reports itself skipped elsewhere, never green.
public sealed class FactOnAttribute : FactAttribute
{
    public FactOnAttribute(string platform)
    {
        if (!OperatingSystem.IsOSPlatform(platform)) Skip = $"runs on {platform} only";
    }
}
