// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Exporting;

/// <summary>The file a writer produces, and the only thing a caller asking for an export names:
/// what the reader chose from the menu. A kind with no writer registered is not offered
/// (<see cref="ExportWriters"/>), so there is no entry in the menu with nothing behind it.
/// </summary>
public enum ExportKind
{
    Excel,
    Csv
}
