// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Runtime.CompilerServices;
using Bunit;

namespace NSail.Testing;

// bUnit's one-second default assumes idle iron; the CI runners share their
// machine with sibling jobs, and a wait that loses the scheduling race fails a
// correct test. The widened ceiling is only how long a WRONG assertion holds
// the run — a passing wait returns the moment its condition does — so patience
// here costs a green run nothing. CI-gated: locally the short leash stays, and
// a genuinely hung wait still surfaces fast at a developer's desk.
internal static class BunitWaitTimeout
{
    [ModuleInitializer]
    internal static void Widen()
    {
        if (Environment.GetEnvironmentVariable("CI") == "true")
        {
            BunitContext.DefaultWaitTimeout = TimeSpan.FromSeconds(30);
        }
    }
}
