// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components;

public partial class NsDateTimeField<TValue>
{
    bool _hasPending;
    DateTime? _pending;

    // One instance for the field's whole life, for the reason NsDateField's own copy carries:
    // the vendor compares Converter by reference, and a fresh one per render throws away the
    // text it is holding that nobody could read as a date.
    readonly DateTextConverter _text;

    public NsDateTimeField()
    {
        _text = new DateTextConverter(() => RegionCulture.For(Language));
    }

    static readonly Dictionary<string, object> Unmarked = [];

    static readonly Dictionary<string, object> Marked = new() { ["aria-invalid"] = "true" };

    // How the refused half of a two-box field is marked, and why it is not the vendor's Error
    // parameter: Error draws the mark AND renders the helper container under this box, with an
    // empty line in it (measured, NsDateRefusalSubmitTests) — height spent on words that belong
    // to the date box above. The class is the vendor's own error state, which is what paints the
    // outline, and the splat is the half an eye cannot read: the box announces itself invalid
    // like the date box already does. Both answers are one of two fixed values rather than built
    // per render, for the reason NsFieldBase's own InputAttributes carries — a fresh dictionary
    // reads as a changed parameter and re-renders the vendor input for nothing.
    string? TimeMark => HasError() ? "mud-input-error" : null;

    Dictionary<string, object> TimeAttributes => HasError() ? Marked : Unmarked;

    // Same trap NsDateField documents, and this control embeds the same vendor picker: it
    // re-renders itself off the EditContext validation event (NsFieldBase's own subscription)
    // before the Value round-trips back down as a parameter from the page, and a bare read of
    // Value would show that render's stale date and wipe what was just picked. The optimistic
    // date survives until OnParametersSet confirms the round trip landed.
    DateTime? MudDate => _hasPending ? _pending : ConvertToDateTime(Value)?.Date;

    TimeSpan? MudTime => ConvertToDateTime(Value)?.TimeOfDay;

    // The date half reaching the same treatment NsDateField already carries — the same
    // placeholder and the same text seam. NOT composed as an actual <NsDateField> here: that
    // component's Error/ErrorText/Required-message wiring reads its OWN FieldIdentifier,
    // built from a `For` expression bound to a real model member — nested inside this field
    // with no member of its own to bind (the date is one half of a combined value, not a
    // property a page can point at), it would show no error state at all, silently losing what
    // the raw MudDatePicker below already gets from THIS field's HasError()/GetErrorText(). Fixing
    // that means teaching NsDateField to accept an externally-supplied error state, which is a
    // change to a component this row does not touch — so the wiring is replicated instead.
    string? GetPlaceholder()
    {
        return Placeholder ?? DatePlaceholder.For(RegionCulture.For(Language), Strings);
    }

    // Same order NsDateField takes, and for the same reason: what the box visibly holds
    // answers before what the model would have said about it.
    protected override string? GetErrorText()
    {
        return Refusal() ?? base.GetErrorText();
    }

    // Same words to the form, so the box and the submit refuse for one reason.
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

        if (_hasPending && ConvertToDateTime(Value)?.Date == _pending)
        {
            _hasPending = false;
        }
    }

    // The picked DATE with the time already held. The picker hands its date back at
    // midnight, so reading the time off IT would silently reset the hour every time someone
    // changed the day — which is what this control did until 2026-08-04: it kept the old
    // date and took the picker's 00:00, the exact inverse of what the person asked for.
    // Refused text is skipped for the reason NsDateField's own copy carries, and the damage here
    // is worse: Combine below falls back to DateTime.Today for a missing date, so writing the
    // refusal through would move the appointment to TODAY at the hour already chosen — a real
    // date, in range, that nobody asked for.
    async Task OnDateChanged(DateTime? value)
    {
        _pending = value?.Date;
        _hasPending = true;

        if (_text.Refused is null)
        {
            await SetValue(Combine(value, ConvertToDateTime(Value)?.TimeOfDay ?? TimeSpan.Zero));
        }

        NotifyOwnProblemChanged();
    }

    // Observed one way out only, as NsDateField's own copy explains: read to redraw the refusal
    // and hand it to the form, never written back in — and a readable text is the optimistic
    // date meanwhile, or the redraw hands a day picked off the calendar back its old date.
    void OnTextChanged(string? text)
    {
        if (DateText.TryParse(text, RegionCulture.For(Language), out var read))
        {
            _pending = read.Date;
            _hasPending = true;
        }

        NotifyOwnProblemChanged();

        StateHasChanged();
    }

    // The date already held with the picked TIME — this half was always right.
    Task OnTimeChanged(TimeSpan? value)
    {
        return SetValue(Combine(ConvertToDateTime(Value), value ?? TimeSpan.Zero));
    }

    static DateTime? ConvertToDateTime(TValue? value)
    {
        return value switch
        {
            null => null,
            DateTime dateTime => dateTime,
            DateTimeOffset dateTimeOffset => dateTimeOffset.DateTime,
            _ => null
        };
    }

    static TValue? Combine(DateTime? date, TimeSpan time)
    {
        var combined = (date?.Date ?? DateTime.Today).Add(time);
        var target = typeof(TValue);

        if (target == typeof(DateTime))
        {
            return (TValue)(object)combined;
        }

        if (target == typeof(DateTime?))
        {
            return (TValue)(object)combined;
        }

        if (target == typeof(DateTimeOffset))
        {
            return (TValue)(object)new DateTimeOffset(combined);
        }

        if (target == typeof(DateTimeOffset?))
        {
            return (TValue)(object)new DateTimeOffset(combined);
        }

        return default;
    }
}
