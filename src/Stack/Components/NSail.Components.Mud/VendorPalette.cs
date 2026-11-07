// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Text;
using MudBlazor;

namespace NSail.Components;

// The provider's own generator, run off-render for a scheme it is not lit in: the declarations
// it would emit under :root with IsDarkMode set to the one asked for. Never rendered, so it asks
// no service and no client for anything.
sealed class VendorPalette : MudThemeProvider
{
    VendorPalette(MudTheme theme, bool dark)
    {
        Theme = theme;
        IsDarkMode = dark;
    }

    internal static IReadOnlyList<string> Declarations(MudTheme theme, bool dark)
    {
        var css = new StringBuilder();

        new VendorPalette(theme, dark).GenerateTheme(css);

        return css.ToString()
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }
}
