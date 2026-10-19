// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Icons;

namespace NSail.Exporting;

/// <summary>Icon catalog for the formats this project and <c>NSail.Exporting.Excel</c> write —
/// each <see cref="IExportWriter.Icon"/> names its entry here rather than pasting its own markup,
/// so a reader's "one drawing, one entry" sweep sees these glyphs the same way it sees every
/// other catalog's. A writer a host adds past these two brings its own entry the same way.</summary>
// Material Symbols (Apache 2.0), drawn at the 20px optical size rather than a scaled 24.
// Symbols ships a "0 -960 960 960" viewBox, and an icon reaches the screen through whichever
// control was given it (NsMenuItem's Icon), which renders its own svg fixed at "0 0 24 24" and
// never asks. So every entry carries the mapping itself: scale by 24/960 and drop back into the
// visible quadrant. Self-contained, one line, identical everywhere.
public static class ExportingIcons
{
    /// <summary>Material Symbols <c>table_chart</c>.</summary>
    public static readonly Glyph TableChart =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M744-144H216q-29 0-50.5-21.5T144-216v-528q0-29.7 21.5-50.85Q187-816 216-816h528q29 0 50.5 21.15T816-744v528q0 29-21.5 50.5T744-144ZM216-636h528v-108H216v108Zm96 72h-96v348h96v-348Zm336 0v348h96v-348h-96Zm-72 0H384v348h192v-348Z"/></g>""");

    /// <summary>Material Symbols <c>csv</c>.</summary>
    public static readonly Glyph Csv =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M288-384h72v-48h-72v-96h72v-48h-72q-20.4 0-34.2 13.8Q240-548.4 240-528v96q0 20.4 13.8 34.2Q267.6-384 288-384Zm120 0h96q10.2 0 17.1-6.9 6.9-6.9 6.9-17.1v-72q0-10-6.9-17t-17.1-7h-48v-24h72v-48h-96q-10.2 0-17.1 6.9-6.9 6.9-6.9 17.1v72q0 10 6.9 17t17.1 7h48v24h-72v48Zm208 0h48l56-192h-50l-30 103-30-103h-50l56 192ZM168-192q-29.7 0-50.85-21.16Q96-234.32 96-264.04v-432.24Q96-726 117.15-747T168-768h624q29.7 0 50.85 21.16Q864-725.68 864-695.96v432.24Q864-234 842.85-213T792-192H168Zm0-72h624v-432H168v432Zm0 0v-432 432Z"/></g>""");
}
