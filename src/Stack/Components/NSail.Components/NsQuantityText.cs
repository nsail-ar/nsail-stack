// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components;

/// <summary>A measure as text, the way <c>NsNumericField</c> reads it: the reader's decimal
/// separator and every decimal the figure really has, and no zero it does not. A stock kept to
/// three decimals is still a count of ten, not "10.000".</summary>
public static class NsQuantityText
{
    // A measure carries its column's scale out of the database (a quantity is decimal(18,3)),
    // and both decimal.ToString() and "N3" spend the figure on zeros nobody typed. Only '#'
    // places vanish when empty, and there are as many as a decimal can hold (28 significant
    // digits) because a format that rounds the box also rounds the value: MudNumericField parses
    // the text it wrote back into the binding when the field is left. No grouping on purpose —
    // the same field holds a factura's number, and "12.345" is not the voucher 12345.
    public static readonly string Format = "0." + new string('#', 28);

    public static string Of(decimal quantity)
    {
        return quantity.ToString(Format);
    }
}
