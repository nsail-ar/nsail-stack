// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Harness.Tests;

// A fact that runs only on one platform and reports itself skipped elsewhere, never green. "unix" is
// every platform but Windows.
public sealed class FactOnAttribute : FactAttribute
{
    public FactOnAttribute(string platform)
    {
        Skip = Platform.Skip(platform);
    }
}

// The theory form of FactOn.
public sealed class TheoryOnAttribute : TheoryAttribute
{
    public TheoryOnAttribute(string platform)
    {
        Skip = Platform.Skip(platform);
    }
}

static class Platform
{
    public static string? Skip(string platform)
    {
        var here = platform == "unix" ? !OperatingSystem.IsWindows() : OperatingSystem.IsOSPlatform(platform);
        return here ? null : $"runs on {platform} only";
    }
}
