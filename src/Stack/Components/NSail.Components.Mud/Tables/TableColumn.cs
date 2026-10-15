// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Reflection;
using NSail.Exporting;
using NSail.Localization;

namespace NSail.Components;

/// <summary>One column a table's header declared: its key, the heading the reader sees, what its
/// values are, and how to read one off a row. All four come from the single <c>For</c> expression
/// the header already carries plus the heading it already resolves — nothing about a column is
/// stated twice, and the only thing a screen ever adds is a format no CLR type can state
/// (<see cref="Of"/>).</summary>
public sealed record TableColumn
{
    public required string Key { get; init; }

    public required string Title { get; init; }

    public required ExportFormat Format { get; init; }

    /// <summary>Reads this column's value off a row of the table. Resolved once from the member
    /// the header NAMED: the columns of a list are its header's, so what else a row type happens
    /// to carry is none of an export's business.</summary>
    public required Func<object, object?> Read { get; init; }

    /// <summary>Builds the column from the member a header's For expression pointed at. Null when
    /// that member is not on the type — a For over something other than a plain member — which is
    /// a column an export leaves out rather than a render that fails.</summary>
    public static TableColumn? Of(Type type, string member, string title, string key, ExportFormat? declared, StringManager strings)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(strings);

        var property = type.GetProperty(member, BindingFlags.Public | BindingFlags.Instance);

        if (property is null || !property.CanRead)
        {
            return null;
        }

        return new TableColumn
        {
            Key = key,
            Title = title,
            Format = declared ?? FormatOf(property.PropertyType),

            // An enum reads as its own word, the same one the cell shows: a file that said
            // "Supplier" where the screen said "Proveedor" would be the list in another language
            // than the person who asked for it.
            Read = row =>
            {
                var value = property.GetValue(row);

                return value is Enum word ? strings.Translate(word) : value;
            }
        };
    }

    /// <summary>What a member's own type says its values are. Money and Percent are the two it
    /// cannot say — both are decimals — so a column declaring neither gets Number, and a screen
    /// that means one says so on the header.</summary>
    public static ExportFormat FormatOf(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        var bare = Nullable.GetUnderlyingType(type) ?? type;

        if (bare == typeof(DateTime) || bare == typeof(DateTimeOffset) || bare == typeof(DateOnly))
        {
            return ExportFormat.Date;
        }

        if (bare.IsEnum || bare == typeof(bool) || bare == typeof(char))
        {
            return ExportFormat.Text;
        }

        if (bare == typeof(decimal) || bare == typeof(double) || bare == typeof(float)
            || bare == typeof(int) || bare == typeof(long) || bare == typeof(short) || bare == typeof(byte))
        {
            return ExportFormat.Number;
        }

        return ExportFormat.Text;
    }
}
