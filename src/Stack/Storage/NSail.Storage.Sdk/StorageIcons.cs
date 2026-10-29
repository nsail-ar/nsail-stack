// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Icons;

namespace NSail.Storage;

/// <summary>Icon catalog for where an install stores its objects — the settings section and each
/// provider's <c>IStorageProvider.Icon</c> name their entry here, because a provider is a server
/// assembly and cannot reach <c>NsIcons</c>. A provider a host adds past these two brings its own
/// entry the same way.</summary>
// Material Symbols (Apache 2.0), 20px optical size, each entry carrying the 960-grid to 24
// mapping itself, as every catalog's do.
public static class StorageIcons
{
    /// <summary>Material Symbols <c>cloud</c>.</summary>
    public static readonly Glyph Cloud =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M240-192q-80 0-136-56T48-384q0-76 52-131.5T227-576q23-85 92.5-138.5T480-768q103 0 179 69.5T744-528q70 0 119 49t49 119q0 70-49 119t-119 49H240Zm0-72h504q40 0 68-28t28-68q0-40-28-68t-68-28h-66l-6-65q-7-74-62-124.5T480-696q-64 0-115 38.5T297-556l-14 49-51 3q-48 3-80 37.5T120-384q0 50 35 85t85 35Zm240-216Z"/></g>""");

    /// <summary>Material Symbols <c>folder</c>.</summary>
    public static readonly Glyph Folder =
        new("""<g transform="translate(0,24) scale(0.025)"><path d="M144-192q-29.7 0-50.85-21.15Q72-234.3 72-264v-432q0-29.7 21.15-50.85Q114.3-768 144-768h242l72 72h358q29.7 0 50.85 21.15Q888-653.7 888-624v360q0 29.7-21.15 50.85Q845.7-192 816-192H144Zm0-72h672v-360H428l-72-72H144v432Zm0 0v-432 432Z"/></g>""");
}
