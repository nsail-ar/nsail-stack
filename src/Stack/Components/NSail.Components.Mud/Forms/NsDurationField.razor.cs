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

    // Duration is the numeric family too: an hour count typed over, same as any other MudNumericField. Which is also why
    // it owes the same culture: half an hour is "0,5" to an es-AR hand, and the vendor's
    // invariant default read that as 5.
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

    // A span TimeSpan cannot hold is refused on the field and the model keeps what it had.
    async Task OnHoursChanged(double? value)
    {
        if (value is { } hours && (double.IsNaN(hours) || Math.Abs(hours) > TimeSpan.MaxValue.TotalHours))
        {
            if (!_tooLong)
            {
                _tooLong = true;
                _refusedOver = Value;
                NotifyOwnProblemChanged();
            }

            return;
        }

        var lifted = _tooLong;
        _tooLong = false;

        await SetValue(FromHours(value));

        // After the write, so the lift is judged against the value the box now holds.
        if (lifted)
        {
            NotifyOwnProblemChanged();
        }
    }

    bool _tooLong;

    TValue? _refusedOver;

    // A value the page writes behind the field replaces what the refusal was about; only a
    // keystroke can raise it, so this is the one seam that can lift it otherwise.
    protected override void OnParametersSet()
    {
        var lifted = _tooLong && !EqualityComparer<TValue?>.Default.Equals(Value, _refusedOver);

        if (lifted)
        {
            _tooLong = false;
        }

        base.OnParametersSet();

        if (lifted)
        {
            NotifyOwnProblemChanged();
        }
    }

    protected override string? GetErrorText()
    {
        return GetOwnProblem() ?? base.GetErrorText();
    }

    protected override string? GetOwnProblem()
    {
        return _tooLong ? Strings.Translate("Components.NsDurationField.TooLong", "That duration is too long") : null;
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
