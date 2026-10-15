// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.DependencyInjection;

namespace NSail.Exporting;

public static class Setup
{
    /// <summary>The table model, the registry and the one writer that costs no dependency.
    /// Excel comes from NSail.Exporting.Excel, which a host adds beside this one — the split is
    /// what keeps a spreadsheet library out of a surface that only ever wanted the text file.
    /// </summary>
    public static IServiceCollection AddExporting(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IExportWriter, CsvExportWriter>();
        services.AddSingleton<ExportWriters>();

        return services;
    }
}
