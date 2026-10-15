// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Collections;
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

            Read = row => Value(property.GetValue(row), strings)
        };
    }

    /// <summary>What goes in the cell. Four kinds of value are not themselves:
    /// <para>An ENUM reads as its own word, the one the cell shows — a file saying "Supplier"
    /// where the screen said "Proveedor" is the list in another language than the person who asked
    /// for it.</para>
    /// <para>A BOOLEAN reads as the Sí or No the cell prints, which is the same promise: True
    /// under a heading that says Activo is the CLR's word for it and nobody else's.</para>
    /// <para>A timestamp the server STAMPED reads in the reader's own clock, because that is
    /// what the cell beside it shows: every screen in the house prints a UTC moment through
    /// ToLocalTime, and a file three hours off the list it came from is nobody's reading. Only a
    /// moment that says it is UTC — a DateTime of any other kind is already local or is a date
    /// with no zone to it.</para>
    /// <para>A COLLECTION reads as its members, comma-separated, because no cell type holds a list
    /// and a column of them is the one the screen already joins by hand (a party's roles). Left
    /// alone it would print the type's name.</para></summary>
    public static object? Value(object? value, StringManager strings)
    {
        ArgumentNullException.ThrowIfNull(strings);

        if (value is Enum word)
        {
            return strings.Translate(word);
        }

        if (value is bool flag)
        {
            return strings.Translate(flag ? "Common.Yes" : "Common.No");
        }

        if (value is DateTime { Kind: DateTimeKind.Utc } stamped)
        {
            return stamped.ToLocalTime();
        }

        if (value is IEnumerable members and not string)
        {
            return string.Join(", ", members.Cast<object?>().Select(member => Value(member, strings)));
        }

        return value;
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
