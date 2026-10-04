// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using NSail.Dates;
using NSail.Messaging.Runtime.Validation;

namespace NSail.Components;

public partial class NsDateField<TValue>
{
    bool _hasPending;
    DateTime? _pending;

    // One instance for the field's whole life, never rebuilt per render: the vendor compares
    // its Converter parameter by reference and re-points the picker at a fresh one as if the
    // rules had changed, and the text this one is holding on to — what somebody typed that is
    // not a date — would go with it. The culture is read through the closure instead, so a
    // language change reaches the same instance.
    readonly DateTextConverter _text;

    public NsDateField()
    {
        _text = new DateTextConverter(() => RegionCulture.For(Language));
    }

    // The vendor picker re-renders itself off the EditContext validation event (NsFieldBase's
    // own subscription) before the Value round-trips back down as a parameter from the page —
    // a bare `ConvertToDateTime(Value)` would read that one render's stale null and, because
    // MudDatePicker reformats its Text on every externally-set Date, wipe what was just typed.
    // The optimistic value survives until OnParametersSet confirms the round trip landed.
    DateTime? MudDate => _hasPending ? _pending : ConvertToDateTime(Value);

    // The calendar's last selectable day is derived from the bound member's own [NotFuture],
    // never named by a page: the field that refuses a date must not offer it first, and a
    // bound written beside the declaration is a second copy that drifts. A member declaring
    // nothing keeps the vendor's unbounded calendar, and a value already past the bound still
    // displays — MudDatePicker reads MaxDate when a day is picked or typed, not when one is
    // handed in (measured, NsCodedRefusalTests), so a row on file with a future date opens
    // showing it instead of silently emptying itself.
    DateTime? MudMaxDate => Declared<NotFutureAttribute>() is null ? null : BusinessDate.Today.ToDateTime(TimeOnly.MinValue);

    // The field says what shape it takes before anything is typed — the active culture's own
    // field order and separators, the active language's own letters. A caller's own words
    // still win.
    string? GetPlaceholder()
    {
        return Placeholder ?? DatePlaceholder.For(RegionCulture.For(Language), Strings);
    }

    // Text the box could not be read as a date answers before anything the model has to say:
    // it is about what is on the screen in front of the person, and a "required" under a box
    // that visibly holds something answers a question nobody asked. Read straight off the seam
    // that raised it rather than through the form, so a field standing outside one still says it.
    protected override string? GetErrorText()
    {
        return Refusal() ?? base.GetErrorText();
    }

    // The same words the form is handed, so the box and the submit refuse for one reason.
    protected override string? GetOwnProblem()
    {
        return Refusal();
    }

    string? Refusal()
    {
        return DateText.Refusal(_text.Refused, RegionCulture.For(Language), Strings);
    }

    protected override void OnParametersSet()
    {
        base.OnParametersSet();

        if (_hasPending && ConvertToDateTime(Value) == _pending)
        {
            _hasPending = false;
        }
    }

    // A box the converter refused is NOT a box somebody emptied, and the vendor raises the same
    // DateChanged(null) for both — only the converter can tell them apart, and it already has.
    // Writing that null through replaces what the model holds with a date NOBODY ENTERED: a
    // non-nullable binding takes default(DateOnly), which is 0001-01-01 and looks like a date all
    // the way to the server. Left alone, the model keeps what it held and the refusal below
    // stops the submit, so the screen and the saved row cannot disagree.
    async Task OnDateChanged(DateTime? value)
    {
        _pending = value;
        _hasPending = true;

        if (_text.Refused is null)
        {
            await SetValue(ConvertFromDateTime(value));
        }

        NotifyOwnProblemChanged();
    }

    // Observed ONE WAY OUT ONLY: Text is never written back in, because a write mid-typing
    // overwrites what is being typed. The read is a redraw and a re-post of the refusal, nothing
    // else — the refusal above is the seam's own state and no parameter carries it down, and the
    // vendor debounces its date event for 100ms after the last one, so a box that holds nothing
    // and is left holding text nobody can read may raise no date event at all: without this the
    // form would never hear about that one.
    //
    // The same debounce is why a readable text becomes the optimistic date right here: a day
    // picked off the calendar writes the text at once and its date 100ms later, and the redraw
    // below would hand the picker the date the model still holds in between — which it takes as
    // set from outside, putting the old day back over the one just picked.
    void OnTextChanged(string? text)
    {
        if (DateText.TryParse(text, RegionCulture.For(Language), out var read))
        {
            _pending = read;
            _hasPending = true;
        }

        NotifyOwnProblemChanged();

        StateHasChanged();
    }

    static DateTime? ConvertToDateTime(TValue? value)
    {
        return value switch
        {
            null => null,
            DateTime dateTime => dateTime.Date,
            DateOnly dateOnly => dateOnly.ToDateTime(TimeOnly.MinValue),
            DateTimeOffset dateTimeOffset => dateTimeOffset.DateTime.Date,
            _ => null
        };
    }

    static TValue? ConvertFromDateTime(DateTime? value)
    {
        if (value is null)
        {
            return default;
        }

        var target = typeof(TValue);

        if (target == typeof(DateTime))
        {
            return (TValue)(object)value.Value;
        }

        if (target == typeof(DateTime?))
        {
            return (TValue)(object)value;
        }

        if (target == typeof(DateOnly))
        {
            return (TValue)(object)DateOnly.FromDateTime(value.Value);
        }

        if (target == typeof(DateOnly?))
        {
            return (TValue)(object)DateOnly.FromDateTime(value.Value);
        }

        if (target == typeof(DateTimeOffset))
        {
            return (TValue)(object)new DateTimeOffset(value.Value);
        }

        if (target == typeof(DateTimeOffset?))
        {
            return (TValue)(object)new DateTimeOffset(value.Value);
        }

        return default;
    }
}
