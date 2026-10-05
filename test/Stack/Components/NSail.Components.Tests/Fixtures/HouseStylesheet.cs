// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components.Tests.Fixtures;

/// <summary>The house's own stylesheet, as text. bUnit paints nothing, so a promise that lives in
/// CSS — a fill DERIVED from the tenant's accent, an ink the stylesheet must NOT define, the
/// token a marked row reads — is checked by reading the file the app ships rather than by a
/// pixel.</summary>
internal static class HouseStylesheet
{
    const string Path = "src/Stack/Components/NSail.Components.Mud/wwwroot/ns-mud.css";

    /// <summary>Newlines normalized, so an assertion about two consecutive declarations reads the
    /// same on a checkout that converted them.</summary>
    internal static string Read()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(System.IO.Path.Combine(directory.FullName, "NSail.sln")))
            {
                return File.ReadAllText(System.IO.Path.Combine(directory.FullName, Path))
                    .ReplaceLineEndings("\n");
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException($"NSail.sln was not found above {AppContext.BaseDirectory}.");
    }
}
