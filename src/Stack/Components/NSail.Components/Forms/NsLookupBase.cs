// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using NSail.Messaging;
using NSail.Messaging.Runtime.Context;
using NSail.References;

namespace NSail.Components;

/// <summary>Base for a kit's `*Lookup`: the control that picks one entity from an
/// unbounded set. It owns everything such a control does not get to decide — the parameter
/// block a screen binds, whether the create entry is allowed, where that entry points, and
/// adopting the row a create form just saved. A facade adds only its concept: the lookup
/// message, the create page, the saved event and its own filters.</summary>
public abstract class NsLookupBase<TItem, TValue, TSaved> : NsPickerBase
    where TItem : IRef
    where TSaved : ISaved
{
    // One token per mounted lookup, minted here rather than derived from anything the screen
    // can repeat: two PartyLookups on one form are two askers, and only the one whose create
    // page was opened may take the row that page saved.
    readonly string _token = Guid.NewGuid().ToString("N");

    bool _allowed;

    [Inject]
    protected PageGate Gate { get; init; } = default!;

    [Inject]
    MessageContextAccessor Delivery { get; init; } = default!;

    [CascadingParameter]
    Task<AuthenticationState>? AuthenticationState { get; set; }

    // NsFieldBase.SetValue is what a click on a real option runs through, and it is what
    // tells the EditContext a field changed. A save this picker adopts never touches it —
    // ValueChanged is invoked directly, below — so without this the model gets the new id
    // and Guardar never learns anything happened (nsail#1569).
    [CascadingParameter]
    EditContext? EditContext { get; set; }

    [Parameter]
    public TValue? Value { get; set; }

    [Parameter]
    public EventCallback<TValue?> ValueChanged { get; set; }

    [Parameter]
    public Expression<Func<TValue>>? ValueExpression { get; set; }

    [Parameter]
    public Expression<Func<TValue>>? For { get; set; }

    [Parameter]
    public string? Label { get; set; }

    [Parameter]
    public string? Placeholder { get; set; }

    [Parameter]
    public string? Helper { get; set; }

    [Parameter]
    public bool Required { get; set; }

    [Parameter]
    public bool Create { get; set; }

    /// <summary>The second create mode: instead of leaving for a create page, the dropdown's
    /// no-results slot offers the text that matched nothing as a row to make, and the field
    /// selects what comes back. For a table whose rows are a name and a parent — a locality under
    /// a province — the page would ask for nothing the typing did not already say, and leaving the
    /// form to open it is what free text was being kept for.</summary>
    [Parameter]
    public bool QuickAdd { get; set; }

    /// <summary>Where the create affordance opens — the same vocabulary NsLink's own
    /// Target carries, handed through to the entry's own link. Defaults to Surfaces.Auto
    /// (one surface further out than where the lookup stands); a caller overrides it exactly
    /// like it would override any link's target.</summary>
    [Parameter]
    public Surface? Target { get; set; } = Surfaces.Auto;

    [Parameter]
    public RenderFragment<TItem>? ItemTemplate { get; set; }

    /// <summary>Null where the concept has no create page at all — a table whose rows are made
    /// from the dropdown itself (QuickAdd) never grew one, and inventing an empty page for it
    /// would be the only reason it exists.</summary>
    protected virtual Type? CreatePage
    {
        get { return null; }
    }

    /// <summary>Route values the create page's own template needs — a concept created only
    /// in someone's context (a Prescription belongs to a Party) has them in its route, and
    /// the facade is the only thing that knows which of its filters they are.</summary>
    protected virtual object? CreateParameters
    {
        get { return null; }
    }

    /// <summary>The create page's own route, raw — the entry's link resolves it against the
    /// surface this lookup stands on, which the dropdown's fragment carries across the popover
    /// portal (intentional-ui-cases.md). Null until the gate has allowed the create page, which
    /// is what keeps the entry out of the dropdown for a session that could not open it
    /// anyway, and null on a lookup nobody may write to: a field nobody may write to asks
    /// nothing (ui/fields.md), and a row created into a field that cannot take it is the
    /// clearest case of it. That covers the door the empty picker draws too, since NsMissing
    /// needs this same route.</summary>
    protected string? CreateRoute
    {
        get
        {
            if (!Create || !_allowed || CreatePage is null || IsReadOnly || IsDisabled)
            {
                return null;
            }

            return WithToken(Routes.GetUrl(CreatePage, CreateParameters));
        }
    }

    // The token rides the URL the create entry already builds — one more query value, written
    // by the same writer that reads it back, so nothing about the trip is hand-composed. It is
    // added after the route table resolved the page's own parameters rather than passed among
    // them: a create page's route tokens are the facade's business, and this one is never one
    // of them.
    string WithToken(string url)
    {
        var mark = url.IndexOf('?', StringComparison.Ordinal);
        var path = mark < 0 ? url : url[..mark];
        var query = mark < 0 ? string.Empty : url[mark..];

        return path + SurfaceQuery.SetValue(query, MessageHeaders.Source, _token);
    }

    protected override void OnInitialized()
    {
        // A lookup may bind something other than the row's key — the policy audience editor
        // binds a Role by Code, because a session carries roles by code. A saved event
        // carries an id and nothing else, so for those bindings the new row simply cannot
        // become this field's value; not subscribing says that, where subscribing anyway
        // would throw an InvalidCastException on every save the app publishes.
        if (!CanTakeId)
        {
            return;
        }

        // Whatever the create form THIS lookup opened saves becomes this field's value; the
        // autocomplete's own OnLoad resolves the display, so nothing here carries text around.
        // Every party-typed lookup on the screen hears the same PartySaved — who adopts it is
        // decided by the token the delivery carries, never by who was listening.
        Subscribe<TSaved>((message, _) =>
        {
            if (!IsMine())
            {
                return Task.CompletedTask;
            }

            return Adopt(Convert(message.Id));
        });
    }

    // The value the field itself resolves via OnParametersSet; the EditContext is this
    // method's own business, and it needs the same identifier NsFieldBase derives (For
    // wins over ValueExpression there too).
    async Task Adopt(TValue value)
    {
        await ValueChanged.InvokeAsync(value);

        if (EditContext is not null && (For ?? ValueExpression) is { } expression)
        {
            EditContext.NotifyFieldChanged(FieldIdentifier.Create(expression));
        }
    }

    // A save with no token was asked for by nobody — the parties grid's own create page, a
    // handler's event — so no lookup adopts it: silence beats writing a row into a field the
    // user never opened.
    bool IsMine()
    {
        return Delivery.Context?.GetHeader(MessageHeaders.Source) == _token;
    }

    protected override async Task OnParametersSetAsync()
    {
        if (Create && CreatePage is not null)
        {
            _allowed = await Gate.Allows(CreatePage, AuthenticationState);
        }
    }

    /// <summary>What the facade answers the escape hatch with — its own create message, sent under
    /// whatever parent the screen bound it to. The Stack names no message and no concept: it hands
    /// over the text and takes back a row. A facade that does not override this offers nothing,
    /// which is why QuickAdd alone cannot turn the entry on.</summary>
    protected virtual Task<TItem?> QuickCreate(string text, CancellationToken cancellationToken)
    {
        return Task.FromResult<TItem?>(default);
    }

    /// <summary>What the field is handed, or nothing at all: a facade whose parent is not answered
    /// yet has nowhere to create under, and an entry that would refuse the click is worse than no
    /// entry (principles.md — remove the possibility, not the instance). A lookup nobody may write
    /// to offers nothing either, the same sentence CreateRoute answers to — the writability read is
    /// kept here rather than in CanQuickAdd, which a facade overrides for its own reason and would
    /// drop it.</summary>
    protected EventCallback<QuickAddEventArgs<TItem>> QuickAddHandler
    {
        get
        {
            if (!CanQuickAdd || IsReadOnly || IsDisabled)
            {
                return default;
            }

            return EventCallback.Factory.Create<QuickAddEventArgs<TItem>>(this, OnQuickAdd);
        }
    }

    protected virtual bool CanQuickAdd
    {
        get { return QuickAdd; }
    }

    async Task OnQuickAdd(QuickAddEventArgs<TItem> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var item = await QuickCreate(args.Text, args.CancellationToken);

        if (item is null)
        {
            return;
        }

        args.Item = item;

        await ValueChanged.InvokeAsync(ItemValue(item));
    }

    protected static bool CanTakeId
    {
        get
        {
            return (Nullable.GetUnderlyingType(typeof(TValue)) ?? typeof(TValue)) == typeof(Guid);
        }
    }

    protected virtual TValue Convert(Guid id)
    {
        return (TValue)(object)id;
    }

    protected virtual TValue ItemValue(TItem item)
    {
        return Convert(item.Id);
    }
}
