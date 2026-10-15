// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Exporting;

/// <summary>What a column's values ARE, which is what a writer needs to put them in a file as
/// the thing they are rather than as their printed form: a figure stays a figure and a date a
/// date in the spreadsheet, and the pattern each is shown under is the writer's to choose.
/// <para>Money and Percent are not derivable from the CLR type — both are decimals — so a
/// column declares them; everything else follows from the member the column names.</para>
/// </summary>
public enum ExportFormat
{
    Text,
    Number,
    Money,
    Date,
    Percent
}
