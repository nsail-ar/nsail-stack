// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.AspNetCore.Components;
using NSail.Localization;

namespace NSail.Components;

public partial class NsDateTimeField<TValue>
{
    bool _hasPending;
    DateTime? _pending;
    DateTime? _displaced;

    bool _hasPendingTime;
    TimeSpan? _pendingTime;
    TimeSpan? _displacedTime;
    bool _restoringTime;

    // One instance for the field's whole life, for the reason NsDateField's own copy carries:
    // the vendor compares Converter by reference, and a fresh one per render throws away the
    // text it is holding that nobody could read as a date.
    readonly DateTextConverter _text;

    readonly TimeTextConverter _time = new();

    // Bound with no receiver, so raising it re-renders nothing on its own: the vendor's Time
    // setter raises TimeChanged(null) on EVERY render while its box holds text it could not
    // read, and the render an ordinary callback adds after each one is the next render — a loop
    // that never yields. OnTimeChanged renders when it has something new to show.
    readonly EventCallback<TimeSpan?> _timeChanged;

    public NsDateTimeField()
    {
        _text = new DateTextConverter(() => RegionCulture.For(Language));
        _timeChanged = new EventCallback<TimeSpan?>(null, (Func<TimeSpan?, Task>)OnTimeChanged);
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

    // The hour half's own bet, for the date half's reason and one more: the vendor's Time setter
    // re-applies on every render of this field, so handing it the held hour while a typed one
    // is in flight rewrites the box — and while a refusal stands, the bet is null, which keeps
    // the refused text in the box instead of the hour it displaced.
    TimeSpan? MudTime => _restoringTime ? null
        : _hasPendingTime ? _pendingTime
        : ConvertToDateTime(Value)?.TimeOfDay;

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

    // One field, one sentence: the date's refusal answers first, the hour's when the date reads.
    string? Refusal()
    {
        return DateText.Refusal(_text.Refused, RegionCulture.For(Language), Strings)
            ?? TimeText.Refusal(_time.Refused, Strings);
    }

    // The optimistic date is a bet on the round trip, and it is dropped the moment the bet is
    // settled EITHER WAY: the value coming back as the date picked (it landed), or coming back
    // as anything else at all — a date the page itself moved to, which is a decision that
    // outranks what the box is holding. Compared against the value the pick displaced rather
    // than against the pick, because only "still the old value" means the round trip is still
    // in flight; everything else means the page has spoken. A read of _pending alone cannot
    // tell those apart, and the box kept a day the page had already moved off (pressing "next
    // open day" repainted the free times and left the closed day in the box).
    protected override void OnParametersSet()
    {
        base.OnParametersSet();

        if (_hasPending && ConvertToDateTime(Value)?.Date != _displaced)
        {
            _hasPending = false;
        }

        if (_hasPendingTime && ConvertToDateTime(Value)?.TimeOfDay != _displacedTime)
        {
            _hasPendingTime = false;
        }
    }

    protected override void OnAfterRender(bool firstRender)
    {
        base.OnAfterRender(firstRender);

        if (_restoringTime)
        {
            _restoringTime = false;

            StateHasChanged();
        }
    }

    // What the box is showing right now, kept once per bet: a second pick while one is still
    // in flight is still waiting on the same round trip, so the first pick's displaced value
    // is the one that settles both.
    void TakePending(DateTime? date)
    {
        if (!_hasPending)
        {
            _displaced = ConvertToDateTime(Value)?.Date;
        }

        _pending = date;
        _hasPending = true;
    }

    void TakePendingTime(TimeSpan? time)
    {
        if (!_hasPendingTime)
        {
            _displacedTime = ConvertToDateTime(Value)?.TimeOfDay;
        }

        _pendingTime = time;
        _hasPendingTime = true;
    }

    // The picked DATE with the time already held. The picker hands its date back at
    // midnight, so reading the time off IT would silently reset the hour every time someone
    // changes the day: the old date kept and the picker's 00:00 taken, the exact inverse of
    // what the person asked for.
    // Refused text is skipped for the reason NsDateField's own copy carries, and the damage here
    // is worse: Combine below falls back to DateTime.Today for a missing date, so writing the
    // refusal through would move the appointment to TODAY at the hour already chosen — a real
    // date, in range, that nobody asked for.
    //
    // An echo of the value already held places no bet, NsDateField's own reason.
    async Task OnDateChanged(DateTime? value)
    {
        var combined = Combine(value, ConvertToDateTime(Value)?.TimeOfDay ?? TimeSpan.Zero);

        if (_text.Refused is null && Echoes(combined))
        {
            NotifyOwnProblemChanged();

            return;
        }

        TakePending(value?.Date);

        if (_text.Refused is null)
        {
            await SetPicked(combined);
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
            TakePending(read.Date);
        }

        NotifyOwnProblemChanged();

        StateHasChanged();
    }

    // The date already held with the picked or typed TIME. Refused text writes nothing, for
    // the reason OnDateChanged carries: the vendor hands it over as null, which would otherwise
    // land as midnight.
    async Task OnTimeChanged(TimeSpan? value)
    {
        if (_time.Refused is not null)
        {
            if (_hasPendingTime && _pendingTime is null)
            {
                return;
            }

            TakePendingTime(null);
        }
        else if (value is null)
        {
            // An emptied box: the value always holds an hour, so the one held stands and the
            // box takes it back — midnight here would be an hour nobody entered. The vendor
            // repaints its box only when Time changes (measured), and the held hour is the one it was last
            // handed, so it is handed null for one render and the held hour on the next.
            _hasPendingTime = false;
            _restoringTime = true;
        }
        else
        {
            var combined = Combine(ConvertToDateTime(Value), value.Value);

            if (!Echoes(combined))
            {
                TakePendingTime(value);

                await SetPicked(combined);
            }
        }

        NotifyOwnProblemChanged();

        StateHasChanged();
    }

    // Observed one way out only, as OnTextChanged is: a second unreadable text after a first
    // raises no TimeChanged, and the refusal must still follow it.
    void OnTimeTextChanged()
    {
        NotifyOwnProblemChanged();

        StateHasChanged();
    }

    static DateTime? ConvertToDateTime(TValue? value)
    {
        return value switch
        {
            null => null,
            DateTime dateTime => dateTime,
            DateTimeOffset dateTimeOffset => TimeZoneInfo.ConvertTime(dateTimeOffset, TimeZoneInfo.Local).DateTime,
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
