// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace NSail.Components;

/// <summary>Base of a kit's action outlet: the typed slot other modules contribute to.
/// Self-typed on the concrete outlet, which is what lets DI hand back
/// IActionContributor&lt;TOutlet&gt; without a single reflected generic. It resolves,
/// gates and orders; the derived outlet is one line of markup naming a presenter.</summary>
public abstract class NsActionOutlet<TOutlet, TModel> : NsPartial, IActionOutlet
    where TOutlet : NsActionOutlet<TOutlet, TModel>
{
    bool _loaded;
    TModel? _loadedFor;
    Task<IReadOnlyList<ActionItem>>? _asking;

    [Inject]
    IEnumerable<IActionContributor<TOutlet>> Contributors { get; init; } = default!;

    [Inject]
    PageGate Gate { get; init; } = default!;

    [CascadingParameter]
    Task<AuthenticationState>? AuthenticationState { get; set; }

    /// <summary>The model this outlet shows actions for — what contributors read.</summary>
    [Parameter, EditorRequired]
    public TModel Context { get; set; } = default!;

    /// <summary>The page hosting this outlet instance, when the same outlet renders inside
    /// more than one page and a contributor's answer depends on which one — a destination the
    /// host already covers on its own (a lupa, a name link) should not contribute itself a
    /// second time there. Optional: null on any page that never needs to tell contributors
    /// apart, and read only by the contributors that care (outlet.Host, since the outlet
    /// instance is what GetActions receives).</summary>
    [Parameter]
    public Type? Host { get; set; }

    /// <summary>The host's own fixed verbs, handed to the presenter ahead of the
    /// contributions so a row is one action cell and not a strip of separate ones
    /// (intentional-ui, grids). Passed through untouched: they are the host's, already
    /// weighted by it, and gating a link is the presenter's single gate either way.</summary>
    [Parameter]
    public IReadOnlyList<ActionItem> Leading { get; set; } = [];

    /// <summary>The row's constant — its ficha, or its edit where there is none. Always
    /// visible, always last, never folded into the overflow.</summary>
    [Parameter]
    public ActionItem? Constant { get; set; }

    /// <summary>The cell's cap, handed to the presenter: how many verbs show as icons before
    /// the rest fold into the kebab. The host's, not the outlet's — a contribution never
    /// widens the row it lands in.</summary>
    [Parameter]
    public int MaxVisible { get; set; } = 3;

    /// <summary>Fired once the contributors have answered, with whether any of them offered an
    /// action — the host's own way to fold a slot that turned out to have nothing in it, the
    /// same call a drawer group left with no children makes.</summary>
    [Parameter]
    public EventCallback<bool> HasActionsChanged { get; set; }

    protected IReadOnlyList<ActionItem> Actions { get; private set; } = [];

    /// <summary>Whether the contributors are still answering — what the presenter draws its
    /// loading mark from, so a contribution that has to ask something slow holds the slot it
    /// is going to fill instead of appearing in it unannounced. The host's own verbs
    /// (Leading, Constant) are untouched by it: they are there from the first frame.</summary>
    protected bool Resolving { get; private set; }

    protected override async Task OnParametersSetAsync()
    {
        if (_loaded && Equals(_loadedFor, Context))
        {
            return;
        }

        _loaded = true;
        _loadedFor = Context;

        var asking = Ask();

        // Only an ask that yields is announced: one that answers within this same synchronous
        // run is adopted before the renderer draws anything, so the row that had nothing to
        // wait for never shows a mark (NsPartial.Handoff turns on the same property).
        Resolving = !asking.IsCompleted;
        _asking = asking;

        var answered = await asking;

        // A Context that moved mid-flight has started an ask of its own: this answer is the
        // model that is no longer here, and the mark belongs to the ask still running.
        if (!ReferenceEquals(_asking, asking))
        {
            return;
        }

        _asking = null;
        Actions = answered;
        Resolving = false;

        if (HasActionsChanged.HasDelegate)
        {
            await HasActionsChanged.InvokeAsync(Actions.Count > 0);
        }
    }

    async Task<IReadOnlyList<ActionItem>> Ask()
    {
        // Every contributor is asked before any of them has answered, so two slow ones cost
        // the slower and not the sum. Order survives: WhenAll answers in the order it was
        // handed, and a contributor that shares one read across the rows of a render pass
        // still sees every ask arrive before the first answer.
        var contributed = await Task.WhenAll(Contributors.Select(contributor => contributor.GetActions((TOutlet)this)));

        var allowed = new List<ActionItem>();

        foreach (var item in contributed.SelectMany(items => items))
        {
            // A link is gated by the page it opens, a command is the contributor's own
            // call: an action nobody could follow never renders.
            if (item.PageType is null || await Gate.Allows(item.PageType, AuthenticationState))
            {
                allowed.Add(item);
            }
        }

        return allowed.OrderBy(item => item.Weight).ToList();
    }
}
