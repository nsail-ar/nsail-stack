// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components;

/// <summary>A figure as text, the way <c>NsMoneyField</c> reads it: two decimals in the reader's
/// grouping, and a currency code after it only when the caller names one — which it does only
/// for an amount that is not in the install's own currency.</summary>
public static class NsMoneyText
{
    public static string Of(decimal amount, string? currency = null)
    {
        return currency is { Length: > 0 } ? $"{amount:N2} {currency}" : amount.ToString("N2");
    }
}
