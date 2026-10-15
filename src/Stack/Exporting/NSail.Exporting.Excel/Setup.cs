// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.DependencyInjection;

namespace NSail.Exporting.Excel;

public static class Setup
{
    /// <summary>Adds the .xlsx writer to whatever <c>AddExporting</c> already registered. Beside
    /// it rather than inside it: this is the half that costs a spreadsheet library, and a surface
    /// that only wants the text file should not carry one.</summary>
    public static IServiceCollection AddExcelExport(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IExportWriter, ExcelExportWriter>();

        return services;
    }
}
