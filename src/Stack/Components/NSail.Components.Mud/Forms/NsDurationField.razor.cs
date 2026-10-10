// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Globalization;
using Microsoft.AspNetCore.Components;
using NSail.Localization;

namespace NSail.Components;

public partial class NsDurationField<TValue>
{
    static readonly double? Minimum = 0;

    readonly FigureConverter<double?> _figures = new();

    [Inject]
    LanguageProvider Language { get; init; } = default!;

    // Duration is the numeric family too (Story: "los campos se seleccionan al recibir
    // foco") — an hour count typed over, same as any other MudNumericField. Which is also why
    // it owes the same culture: half an hour is "0,5" to an es-AR hand, and the vendor's
    // invariant default read that as 5 (nsail#1620).
    protected override bool SelectOnFocus => true;

    CultureInfo FigureCulture
    {
        get { return RegionCulture.For(Language); }
    }

    string Unit
    {
        get { return Strings.Translate("Common.Hours", "h"); }
    }

    double? Hours
    {
        get { return Value is TimeSpan span ? span.TotalHours : null; }
    }

    Task OnHoursChanged(double? value)
    {
        return SetValue(FromHours(value));
    }

    static TValue? FromHours(double? value)
    {
        if (value is null)
        {
            return default;
        }

        var target = typeof(TValue);

        if (target == typeof(TimeSpan) || target == typeof(TimeSpan?))
        {
            return (TValue)(object)TimeSpan.FromHours(value.Value);
        }

        return default;
    }
}
