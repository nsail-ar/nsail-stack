// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.JSInterop;
using NSail.Icons;

namespace NSail.Components;

/// <summary>
/// Navigation context of the surface a component renders in: the default page
/// (Name null) or a named overlay ("aside", "modal"). Navigation targets the
/// surface itself — full-page on the default, the ?name= query parameter on
/// named ones.
/// </summary>
public sealed class SurfaceContext
{
    readonly object _sync = new();
    readonly HashSet<object> _dirtySources = [];
    readonly List<SaveWindow> _windows = [];
    readonly List<ActClaim> _acts = [];
    readonly System.Runtime.CompilerServices.ConditionalWeakTable<object, object> _retired = new();
    readonly NavigationManager _navigation;
    readonly RouteTable _routeTable;
    readonly IJSRuntime _js;
    readonly SurfaceHistory _history;
    readonly Surface? _name;
    readonly Action? _close;
    string? _title;
    Glyph? _icon;
    RenderFragment? _utilities;
    RenderFragment? _index;
    object? _refusingSource;
    object? _toggleSource;
    int _workCount;
    volatile bool _hasWork;
    volatile bool _hasChanges;
    volatile bool _formDisabled;
    volatile bool _formRunning;
    volatile bool _finishing;
    bool _closesOnArrival;
    bool _popping;

    internal SurfaceContext(
        Surface? name,
        NavigationManager navigation,
        RouteTable routeTable,
        IJSRuntime js,
        SurfaceHistory history,
        Action? close = null)
    {
        _name = name;
        _navigation = navigation;
        _routeTable = routeTable;
        _js = js;
        _history = history;
        _close = close;
    }

    public Surface? Name => _name;

    /// <summary>True on the main (full-page) surface, false inside a named one (aside,
    /// modal) — pages read it to adapt to the space they get, e.g. SetSize.</summary>
    public bool IsMain => _name is null;

    /// <summary>True on a surface its host closes rather than a route (a dialog): it carries
    /// no query key of its own, nothing links to it, and it was opened programmatically to ask
    /// exactly one thing. A routed overlay is a place the user navigated to and can hold
    /// anything; this one cannot.</summary>
    internal bool IsHosted => _close is not null;

    public bool HasWork => _hasWork;

    public bool HasChanges => _hasChanges;

    /// <summary>What the form on this surface refuses — it was handed Disabled, or its own submit is
    /// in flight and every field under it is frozen for the length of it. The very value that form
    /// cascades as "ParentDisabled", carried here for chrome standing OUTSIDE its cascade: a dialog's
    /// header X is drawn in the vendor's own tree, above the hosted component and the form in it, so
    /// the word travels up here and back down (NsDialogExit). It is not HasWork — that one is the
    /// whole surface's, an NsLoad refresh included, and this is the form's own word, the same one the
    /// footer's Cancelar answers. False on a surface holding no form.</summary>
    public bool FormRefuses => _formDisabled || _formRunning;

    /// <summary>The narrower half of it: that form's own submit is in flight, for what may be
    /// withheld for the length of a save and no longer — a dialog's Escape (NsDialogExit). The
    /// form's word is two words here for the same reason it is two cascades: an OR cannot be taken
    /// back apart once both halves are true.</summary>
    public bool FormRunning => _formRunning;

    /// <summary>Raised when the two words above move. Its own event and not StateChanged, for the
    /// reason IndexChanged is not AnnouncementChanged: the only reader is chrome outside the
    /// form's cascade (NsDialogExit), a save starting and ending is already on StateChanged as
    /// HasWork for everything that draws a spinner, and NsForm reports this from the after-render
    /// of the pass that decided it — so an event the page and the submit also listened to would
    /// make it a render that provokes a render, for a state change already announced.</summary>
    public Action? RefusalChanged;

    // What a departure actually has to be asked about, which is not everything HasChanges counts:
    // a submit in flight answers for every report its own spend reaches, and the window that
    // brackets it is that save saying the departure ahead is its own (NsForm.HandleSubmit ends its
    // handler on what it just wrote). Reports covered by an open window are therefore the save's
    // WHEREVER in its life they were filed — before the window opened as well as inside it — so no
    // ordering between the spend and the window can leave one of them arming the guard against the
    // navigation the save itself makes (nsail#1471). What no open window reaches is somebody's
    // unsaved work and still protests: another FORM's document is its own business (Spendable),
    // and with nothing saving this is HasChanges exactly.
    internal bool HasUnansweredChanges
    {
        get
        {
            lock (_sync)
            {
                if (_windows.Count == 0)
                {
                    return _hasChanges;
                }

                return _dirtySources.Any(source => !_windows.Any(window => Spendable(source, window.Document)));
            }
        }
    }

    /// <summary>What the page on this surface calls itself. Announced, never asked for: the
    /// title bar says it once and whoever draws this surface's chrome reads it here.</summary>
    public string? Title => _title;

    /// <summary>The glyph that names the announced page — derived from the address it was
    /// opened at (NavMenuItem.GlyphFor: the entry for that address, or the nearest ancestor
    /// address that has one), unless the page overrode it. Null only where no address up the
    /// page's own family has a menu entry.</summary>
    public Glyph? Icon => _icon;

    /// <summary>The announced page's utility actions — print, export, share, a link out. The
    /// fragment is the page's own, so what it renders keeps answering to the page whichever
    /// chrome draws it. Acts stay in the page's footer and content actions in its toolbar;
    /// neither travels.</summary>
    public RenderFragment? Utilities => _utilities;

    /// <summary>Raised when the announcement changes. Separate from StateChanged on purpose:
    /// StateChanged re-renders the page itself (NsPage subscribes), and a page whose render
    /// re-announces would be re-rendering itself forever.</summary>
    public Action? AnnouncementChanged;

    /// <summary>The page tells its surface what it is called and what utilities it offers.
    /// NsTitleBar announces on the main surface and still draws its own row there, since the
    /// shell mounts no app bar; the reader is NsAppBar, for a host that mounts one. An overlay
    /// keeps its own chrome, so nothing reads the announcement there; announcing anyway costs
    /// nothing and keeps the component free of a per-surface branch.</summary>
    public void Announce(string? title, Glyph? icon, RenderFragment? utilities)
    {
        if (string.Equals(_title, title, StringComparison.Ordinal)
            && _icon == icon
            && ReferenceEquals(_utilities, utilities))
        {
            return;
        }

        _title = title;
        _icon = icon;
        _utilities = utilities;

        AnnouncementChanged?.Invoke();
    }

    /// <summary>The page's own index — the rail it draws beside its content where there is room
    /// for two columns. Announced so this surface's chrome can draw it where there is not: on the
    /// main surface that chrome is the drawer the nav menu lives in, which is the one gutter the
    /// frame has, so a page's rail collapses on a phone exactly as the nav does. Null on every
    /// page that has no index, which is most of them.</summary>
    public RenderFragment? Index => _index;

    /// <summary>Raised when the announced index changes. Its own event, not AnnouncementChanged:
    /// the app bar has nothing to redraw when an index moves, and the drawer has nothing to
    /// redraw when a title does.</summary>
    public Action? IndexChanged;

    /// <summary>The page hands its index to the surface and stops deciding where it goes. Called
    /// again whenever what the index shows changed — the fragment is the page's own, so the
    /// chrome redraws it without knowing what moved inside it.</summary>
    public void AnnounceIndex(RenderFragment? index)
    {
        if (ReferenceEquals(_index, index))
        {
            return;
        }

        _index = index;

        IndexChanged?.Invoke();
    }

    /// <summary>Content size requested by the rendered page, along the host's own axis: a
    /// side drawer reads it as width, the main container as max-width.</summary>
    public NsSize Size { get; private set; } = NsSize.Md;

    /// <summary>True when the page asks to float over the content (overlay + backdrop)
    /// even where the host would dock. The page can only opt out of docking, not force it.</summary>
    public bool Floating { get; private set; }

    // The source every FLOATING stacking tier derives from — Chain order times Step, offset by
    // Base. NsDialog reads ZIndex per instance; BrandMudTheme reads VendorZIndex once for every
    // MudBlazor overlay that isn't one of our own surfaces (Confirm/Alert, DialogManager.Open,
    // a select's popover). Neither ever states 1300 or 100 again — two places agreeing by
    // construction, not by two people remembering.
    //
    // Base is deliberately MudBlazor's own --mud-zindex-appbar: the app bar is the rung this
    // ladder is measured FROM, so every tier here is a tier above the app's constant chrome.
    // That is why a docked drawer takes none of them (NsDrawer) — a drawer belongs under the
    // bar, and the vendor's --mud-zindex-drawer is where under the bar lives.
    const int BaseZIndex = 1300;
    const int StepZIndex = 100;

    /// <summary>CSS stacking tier for a surface that floats over the frame, derived from its
    /// position in the same escalation Chain Auto resolves through — never a number a host
    /// picks for itself. The surface opened later always paints over the one it was opened
    /// from: Main carries no chrome of its own, Aside and Modal follow Chain order, and a
    /// product's own surface (outside the Chain, nothing escalates past it) takes the outermost
    /// tier. A host whose chrome docks instead of floating — the aside's drawer — reads the
    /// vendor's drawer tier rather than this one, because everything here outranks the app
    /// bar and nothing docked ever may.</summary>
    public int ZIndex => BaseZIndex + (StepZIndex * StackLevel);

    /// <summary>The tier MudBlazor's own overlays render at — Confirm/Alert
    /// (IDialogService.ShowMessageBoxAsync), the component host behind DialogManager.Open
    /// (NsOpenDialog, itself a Surfaces.Dialog instance), a select's popover, a snackbar.
    /// One step beyond the Chain's last entry — the same "outside the Chain, nothing
    /// escalates past it" tier a product's own surface takes (Escalated()) — so a vendor
    /// overlay always outranks whatever surface it was opened from, Main, Aside or Modal,
    /// without knowing which. BrandMudTheme.From is the one place that reads it.</summary>
    public static int VendorZIndex => BaseZIndex + (StepZIndex * (Chain.Length + 1));

    int StackLevel
    {
        get
        {
            if (IsMain)
            {
                return 0;
            }

            var index = Array.IndexOf(Chain, _name!.Value);

            return index < 0 ? Chain.Length + 1 : index + 1;
        }
    }

    public Action? StateChanged;

    public event Func<ProblemEventArgs, Task>? OnProblem;

    public void SetSize(NsSize size)
    {
        if (Size == size)
        {
            return;
        }

        Size = size;
        StateChanged?.Invoke();
    }

    public void SetFloating(bool floating)
    {
        if (Floating == floating)
        {
            return;
        }

        Floating = floating;
        StateChanged?.Invoke();
    }

    // The default surface outlives navigation (only named surfaces remount per route),
    // so each page load restores the presentation defaults before OnCreated runs.
    internal void ResetView()
    {
        // The announcement goes with them: the arriving page announces from its own render, and
        // a page that carries no title bar at all would otherwise leave the previous page's name
        // standing in the chrome.
        Announce(null, null, null);
        AnnounceIndex(null);

        if (Size == NsSize.Md && !Floating)
        {
            return;
        }

        Size = NsSize.Md;
        Floating = false;
        StateChanged?.Invoke();
    }

    public void Navigate(string route)
    {
        // The surface is about to close on a save that took, so where its own content would
        // move next has no reader. Every create page ends its submit with an in-place
        // navigation to the new record's edit route — main-surface doctrine
        // (create-thin-then-enrich), and inside an overlay the close supersedes it. Dropping
        // it here rather than closing on top of it is what keeps the two from racing: the
        // destination page would otherwise mount, start its own load, and be torn down a
        // frame later.
        if (_finishing)
        {
            return;
        }

        Follow(GetHref(route));
    }

    /// <summary>Brackets the submit of a tracked form that will close this surface if the
    /// server accepts it (NsForm). Only an overlay ever sets it — the main surface stays open
    /// after a save and its pages navigate exactly as they always have.</summary>
    internal void SetFinishing(bool finishing)
    {
        _finishing = finishing;
    }

    /// <summary>How a write that moves nothing the user could go back to lands in browser
    /// history: replacing the current entry. Moving the page inside a surface that is already
    /// open, and writing a tab or a filter onto the address, are views of the place the user
    /// is standing at rather than places of their own — and so is an open the opener marked
    /// NoHistory. Opening a surface that was closed is a place, and pushes.</summary>
    public static NavigationOptions InPlace { get; } = new() { ReplaceHistoryEntry = true };

    // The other half of InPlace, spelled out rather than left to NavigateTo(href): the bare
    // overload routes a non-replacing navigation through NavigateToCore(string, bool) instead
    // of NavigateToCore(string, NavigationOptions), so a NavigationManager that only overrides
    // the options shape — bUnit's, and every fake in the test tree — never sees the call at all.
    static readonly NavigationOptions s_stacked = new();

    /// <summary>Whether navigating to <paramref name="href"/> only redraws surfaces over the
    /// place the user is already at — same path, different surface query.</summary>
    public bool StaysInPlace(string href)
    {
        ArgumentException.ThrowIfNullOrEmpty(href);

        var current = _navigation.ToAbsoluteUri(_navigation.Uri);
        var target = _navigation.ToAbsoluteUri(href);

        return string.Equals(current.AbsolutePath, target.AbsolutePath, StringComparison.Ordinal);
    }

    /// <summary>Navigates to an href this surface already resolved (GetHref), landing it in
    /// browser history the way its destination deserves. Components that hand a user a
    /// resolved href (a link, an action toolbar) navigate through this, passing the
    /// <paramref name="target"/> they resolved it against so the destination surface is named
    /// rather than guessed back out of the address.
    ///
    /// Three transitions, and only the first is the caller's to influence. A surface whose key
    /// the address did not carry and now does was CLOSED and is being opened: that is a place,
    /// and it pushes unless <paramref name="noHistory"/> says the open is a satellite feeding
    /// a form still standing underneath (NsAutocomplete's inline create, NsMissing's door). A
    /// surface already open whose route changes is moving inside one place and always replaces
    /// — no flag changes that, or the back-stack would grow by one per click inside an open
    /// aside. A path change is a different place and pushes, as any navigation does.</summary>
    public void Follow(string href, Surface? target = null, bool noHistory = false)
    {
        ArgumentException.ThrowIfNullOrEmpty(href);

        // A host-managed surface (a dialog) carries no route of its own to keep representing
        // once a link inside it has sent the user elsewhere — there is no query key it could
        // hold to stay open and correct, unlike a named surface. Closing it on the way out,
        // once, is what keeps every dialog-hosted link honest without each one wiring
        // Surface?.Close() itself — but the close is armed here and spent on arrival, never
        // fired on the next line. Navigation is not decided by the time NavigateTo returns:
        // the unsaved-changes guard is a NavigationLock handler that AWAITS its question, so
        // a close fired here tore the dialog down while the person was still reading the
        // protest, which cancels nothing and answers for them. LocationChanged is raised only
        // by a navigation that actually committed, so it is the one signal that knows the
        // answer — and a navigation the guard refused simply never raises it.
        ArmClose();

        if (!StaysInPlace(href))
        {
            _navigation.NavigateTo(href, s_stacked);
            return;
        }

        var opened = Opening(href, target);

        if (opened is null)
        {
            _navigation.NavigateTo(href, InPlace);
            return;
        }

        _history.Opened(opened.Value, pushed: !noHistory);

        _navigation.NavigateTo(href, noHistory ? InPlace : s_stacked);
    }

    /// <summary>The surface <paramref name="href"/> opens that the address does not already
    /// carry, or null when it opens none — a move inside one that is already showing
    /// something, a close, or a write onto the address the user is standing at.</summary>
    Surface? Opening(string href, Surface? target)
    {
        var destination = Destination(target);

        if (destination is null)
        {
            return null;
        }

        var current = _navigation.ToAbsoluteUri(_navigation.Uri);
        var next = _navigation.ToAbsoluteUri(href);

        return !Carries(current, destination.Value) && Carries(next, destination.Value)
            ? destination
            : null;
    }

    // The destination is named by the caller and never diffed back out of the address: a
    // query key appearing is not enough to say a surface opened — a filter or a tab written by
    // a link would read as one — and the only reader who knows for certain is whoever chose
    // the target GetHref resolved the href against.
    Surface? Destination(Surface? target)
    {
        if (target is null)
        {
            return _name;
        }

        if (target.Value.IsBrowser || target == Surfaces.Main)
        {
            return null;
        }

        return target == Surfaces.Auto ? Escalated() : target.Value;
    }

    static bool Carries(Uri uri, Surface surface)
    {
        return SurfaceQuery.TryGetValue(uri.Query, surface.Name, out var route)
            && !string.IsNullOrWhiteSpace(route);
    }

    void ArmClose()
    {
        if (_close is null || _closesOnArrival)
        {
            return;
        }

        _closesOnArrival = true;
        _navigation.LocationChanged += CloseOnArrival;
    }

    void CloseOnArrival(object? sender, LocationChangedEventArgs args)
    {
        Detach();
        _close?.Invoke();
    }

    /// <summary>Spends anything this surface is still waiting on, so a host that is going away
    /// leaves nothing behind: a close armed by a Follow whose navigation never landed — the
    /// person declined the unsaved-changes protest and stayed inside the dialog — would
    /// otherwise be spent by whatever navigates next, and a pop still counted as in flight
    /// would keep a handler on a NavigationManager that outlives this surface.</summary>
    internal void Detach()
    {
        EndPop();

        if (!_closesOnArrival)
        {
            return;
        }

        _closesOnArrival = false;
        _navigation.LocationChanged -= CloseOnArrival;
    }

    public string GetHref(string route)
    {
        ArgumentException.ThrowIfNullOrEmpty(route);

        return IsMain ? Rooted(route) : Query(_name!.Value, route);
    }

    /// <summary>Resolves a link target against this surface: null navigates this surface,
    /// a browser target and Surfaces.Main the rooted route, Surfaces.Auto one surface further
    /// out, and any other name that surface.</summary>
    public string GetHref(string route, Surface? target)
    {
        ArgumentException.ThrowIfNullOrEmpty(route);

        if (target is null)
        {
            return GetHref(route);
        }

        // A browser target opens a document of the browser's own, and that document has no
        // aside and no modal to inherit: a surface query only the CURRENT document knows how
        // to read would arrive there as the app booting at this same address, which loads the
        // page again and never asks for the destination. It gets the rooted route, the address
        // Surfaces.Main gives — the only one that means the same thing in any document.
        if (target.Value.IsBrowser || target == Surfaces.Main)
        {
            return Rooted(route);
        }

        return Query(target == Surfaces.Auto ? Escalated() : target.Value, route);
    }

    // "Further out" is an order, so the chain is a list rather than a switch: main opens
    // the first, each surface opens the next, and the last opens itself so the escalation
    // always resolves to something navigable.
    static readonly Surface[] Chain = [Surfaces.Aside, Surfaces.Modal];

    Surface Escalated()
    {
        if (IsMain)
        {
            return Chain[0];
        }

        var index = Array.IndexOf(Chain, _name!.Value);

        // A product's own surface is outside the Stack's chain, so there is no declared
        // "further out" for it: it navigates itself instead of guessing an order — but only
        // when it CAN be navigated to. A host-managed surface (a dialog, _close set) answers
        // to no query key of its own ("nothing links to it", Surfaces.Dialog) — self-targeting
        // it silently writes a query nobody reads, which is indistinguishable from a dead
        // link. It has exactly as much of "further out" declared for it as Main does, so it
        // escalates the same way Main does.
        if (index < 0)
        {
            return _close is null ? _name!.Value : Chain[0];
        }

        return Chain[Math.Min(index + 1, Chain.Length - 1)];
    }

    string Query(Surface surface, string route)
    {
        var uri = _navigation.GetUriWithQueryParameter(surface.Name, route.TrimStart('/'));

        return Rooted(_navigation.ToBaseRelativePath(uri));
    }

    /// <summary>Whether a destination belongs to somebody else — it carries its own scheme
    /// (https:, mailto:, tel:) — and so passes through untouched by any surface. Everything
    /// else is a route this app resolves.</summary>
    public static bool IsExternal(string href)
    {
        ArgumentException.ThrowIfNullOrEmpty(href);

        // The rooted case is decided BEFORE Uri is asked anything, and that order is the fix:
        // Uri answers "is this absolute" about the operating system's file paths, not about a
        // web app's routes. Every Unix-flavoured runtime reads a leading slash as an implicit
        // file:// path and calls "/parties/new" absolute; Windows does not, but calls
        // "//host/share" absolute all the same. Asking Uri first made the same binary
        // disagree with itself across the prerender boundary — a Windows dev box resolved
        // every link correctly while WASM, and a Linux server's prerender, handed back the raw
        // route with no surface query and no click interception, which navigates fine and so
        // shows nothing. A '/'-rooted value is one of ours by construction.
        return href[0] != '/' && Uri.TryCreate(href, UriKind.Absolute, out _);
    }

    static string Rooted(string route)
    {
        return route.StartsWith('/') ? route : "/" + route;
    }

    /// <summary>The query string of the route THIS surface is routed by. On the main surface
    /// that is the address bar's own; inside a named one it is the query of the inner route,
    /// which travels as part of the VALUE of ?aside=/?modal= and therefore never reaches the
    /// browser's query at all — the exact reason Blazor's [SupplyParameterFromQuery], which
    /// binds from the real URI, reads null for a page opened in an overlay. A host-managed
    /// surface (a dialog) is routed by nothing and carries no query.</summary>
    string OwnQuery
    {
        get
        {
            var uri = _navigation.ToAbsoluteUri(_navigation.Uri);

            return IsMain ? uri.Query : SurfaceQuery.RouteQuery(RouteIn(uri));
        }
    }

    /// <summary>Whether this surface's own route carries <paramref name="name"/> at all.</summary>
    public bool TryGetQuery(string name, out string? value)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        return SurfaceQuery.TryGetValue(OwnQuery, name, out value);
    }

    /// <summary>A query value off this surface's own route, converted the culture-invariant way
    /// the route table wrote it; the default when it is absent or unreadable. An array type
    /// (Guid[]) collects every value the key repeats. Always read live — nothing is cached, so
    /// a page that reads it again from OnParametersSet sees a changed query.</summary>
    public T? GetQuery<T>(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        return SurfaceQuery.GetValue<T>(OwnQuery, name);
    }

    /// <summary>Every raw value this surface's route carries under <paramref name="name"/>.</summary>
    public IReadOnlyList<string> GetQueryValues(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        return SurfaceQuery.GetValues(OwnQuery, name);
    }

    /// <summary>Writes <paramref name="name"/> onto the route THIS surface is routed by — the
    /// write half of GetQuery, and the only door for it. A null value removes the key; the
    /// value is written by the same invariant formatter the URL builder uses, so reading it
    /// back gives what was written.
    ///
    /// Where it lands is the surface's answer, not the caller's: on the main surface the
    /// address bar's own query, inside a named one the query of the inner route, which is
    /// merged IN PLACE inside the value of ?aside=/?modal= and re-encoded exactly once — the
    /// composition never resolves a route it did not read, which is what keeps a query from
    /// nesting inside another (nsail#78). A host-managed surface (a dialog) is routed by
    /// nothing and carries no query, so this is a no-op there rather than a write nobody could
    /// read. Always REPLACES the history entry: a tab or a filter is a view of the place the
    /// user is at, not a place of its own that Back owes them.</summary>
    public void SetQuery(string name, object? value)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        SetQuery(new Dictionary<string, object?> { [name] = value });
    }

    /// <summary>The batched form of SetQuery(name, value): every pair lands in the ONE
    /// navigation this makes. A caller clearing a filter whose own default another filter's
    /// Query() reads off the first one's absence (nsail#1807) cannot write them as two
    /// separate calls — SetQuery reads the surface's own route fresh each time, and the second
    /// call would read it before the first call's navigation landed.</summary>
    public void SetQuery(IReadOnlyDictionary<string, object?> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        if (IsHosted)
        {
            return;
        }

        var uri = _navigation.ToAbsoluteUri(_navigation.Uri);
        var path = uri.GetLeftPart(UriPartial.Path);

        if (IsMain)
        {
            var query = uri.Query;

            foreach (var (name, value) in values)
            {
                query = SurfaceQuery.SetValue(query, name, value);
            }

            _navigation.NavigateTo(path + query, InPlace);
            return;
        }

        var route = RouteIn(uri);

        // Nothing is routed here, so there is no own route to write onto — the surface is not
        // rendering a page at all, and inventing one would open it.
        if (route is null)
        {
            return;
        }

        var routeQuery = SurfaceQuery.RouteQuery(route);

        foreach (var (name, value) in values)
        {
            routeQuery = SurfaceQuery.SetValue(routeQuery, name, value);
        }

        var inner = SurfaceQuery.RoutePath(route) + routeQuery;

        _navigation.NavigateTo(
            path + SurfaceQuery.SetValue(uri.Query, _name!.Value.Name, inner),
            InPlace);
    }

    public void Navigate<T>(object? parameters = null) where T : IComponent
    {
        Navigate(_routeTable.GetUrl<T>(parameters));
    }

    public void Navigate<T>(IReadOnlyDictionary<string, object?> parameters) where T : IComponent
    {
        Navigate(_routeTable.GetUrl<T>(parameters));
    }

    /// <summary>Opens a route in a named surface (e.g. "aside", "modal").</summary>
    public void Open(Surface surface, string route)
    {
        ArgumentException.ThrowIfNullOrEmpty(surface.Name);
        ArgumentException.ThrowIfNullOrEmpty(route);

        Follow(_navigation.GetUriWithQueryParameter(surface.Name, route.TrimStart('/')), surface);
    }

    public void Open<T>(Surface surface, object? parameters = null) where T : IComponent
    {
        Open(surface, _routeTable.GetUrl<T>(parameters));
    }

    /// <summary>Closes this surface: host-managed surfaces (dialogs) close through their
    /// host; named ones by leaving the entry their open pushed, or by removing their query
    /// parameter when the open pushed nothing. No-op on the default surface.</summary>
    public void Close()
    {
        if (_close is not null)
        {
            _close();
            return;
        }

        if (IsMain)
        {
            return;
        }

        // An open that pushed left an entry of its own, and rewriting the address on top of it
        // would stack a second entry over the first rather than undo it — Back from there
        // would reopen the surface the user has just finished with. Popping is what actually
        // spends that entry, and the browser's own back is the only thing that can: it is
        // still guarded, because Blazor restores the history position before it asks the
        // location-changing handlers and leaves it restored if one refuses (blazor.web.js,
        // onBrowserInitiatedPopState), so the X over a dirty form protests exactly as it does
        // when the close is a navigation.
        //
        // Nothing recorded means nothing was pushed — an address pasted fresh or a reload
        // arrives that way — and there the rewrite below is the only close that cannot walk
        // the user out of the app.
        if (_history.Pops(_name!.Value))
        {
            Pop();
            return;
        }

        Follow(_navigation.GetUriWithQueryParameter(_name!.Value.Name, (string?)null));
    }

    // One gesture spends one entry. history.back() is a round trip — JS interop, popstate,
    // circuit, the location-changing handlers, unmount — and every door into Close() is a plain
    // handler with no debounce of its own (NsClose's X, NsCloseOnEscape, NsDrawer's backdrop),
    // so a second close landing inside that window is ordinary: a double-click, or Escape
    // pressed right after the click. The record cannot answer it and must not be made to —
    // SurfaceHistory describes the browser's stack rather than this gesture, and deliberately
    // outlives the close so a surface returned to by Forward still knows its entry is there —
    // and two pops for one click walk the user past the screen the surface was opened from, out
    // of the app entirely when that screen was the first in-app entry.
    void Pop()
    {
        if (_popping)
        {
            return;
        }

        _popping = true;
        _navigation.LocationChanged += PopLanded;

        _ = Back();
    }

    void PopLanded(object? sender, LocationChangedEventArgs args)
    {
        EndPop();
    }

    /// <summary>Ends the pop this surface has in flight, so the next Close() is a gesture of its
    /// own rather than the tail of the last one. Both ends of a pop call it, because a pop is
    /// guarded: LocationChanged is raised only by one that committed, and NsSurfaceContext's own
    /// NavigationLock calls it for one it refused — Blazor restores the history position before
    /// it asks the location-changing handlers and leaves it restored when one says no, so a
    /// dirty surface whose person answers "no" stays open and its X has to keep working.
    /// Suppression that outlived either end would trade an over-pop for a dead X.</summary>
    internal void EndPop()
    {
        if (!_popping)
        {
            return;
        }

        _popping = false;
        _navigation.LocationChanged -= PopLanded;
    }

    /// <summary>Browser history back. Closing a surface goes through Close(), which comes here
    /// only for a surface whose own open pushed the entry underneath it — called directly it
    /// leaves the site on a deep link.</summary>
    public ValueTask Back()
    {
        return _js.InvokeVoidAsync("history.back");
    }

    public void Enter()
    {
        var raiseChanged = false;

        lock (_sync)
        {
            if (_workCount++ == 0)
            {
                _hasWork = true;
                raiseChanged = true;
            }
        }

        if (raiseChanged)
        {
            StateChanged?.Invoke();
        }
    }

    public void Exit()
    {
        var raiseChanged = false;

        lock (_sync)
        {
            if (_workCount == 0)
            {
                return;
            }

            if (--_workCount == 0)
            {
                _hasWork = false;
                raiseChanged = true;
            }
        }

        if (raiseChanged)
        {
            StateChanged?.Invoke();
        }
    }

    /// <summary>Whether a navigation to <paramref name="targetLocation"/> actually takes this
    /// surface's own document away. Opening an aside over a tracked form is an internal
    /// navigation — it writes the aside's route into the address as a query parameter — and
    /// every surface in the tree hears it, so the page underneath was asking about unsaved
    /// changes it was not about to lose. A surface only leaves when the PATH of the address it
    /// is routed by changes — the address bar's for the main surface, the path of its own inner
    /// route for a named one. Identity lives in a path; a query is filter or component state, so
    /// a screen writing its own tab or search onto the address it is standing at has not
    /// moved.</summary>
    internal bool Departs(string targetLocation)
    {
        // A host-managed surface (a dialog) carries no route in the address at all, so there
        // is nothing here to compare: the only navigation it can witness is the page
        // underneath moving, which takes the host with it.
        if (_close is not null)
        {
            return true;
        }

        var current = _navigation.ToAbsoluteUri(_navigation.Uri);
        var target = _navigation.ToAbsoluteUri(targetLocation);

        if (IsMain)
        {
            return !string.Equals(current.AbsolutePath, target.AbsolutePath, StringComparison.Ordinal);
        }

        return !string.Equals(
            SurfaceQuery.RoutePath(RouteIn(current)),
            SurfaceQuery.RoutePath(RouteIn(target)),
            StringComparison.Ordinal);
    }

    string? RouteIn(Uri uri)
    {
        return SurfaceQuery.Parse(uri.Query).TryGetValue(_name!.Value.Name, out var route)
            && !string.IsNullOrWhiteSpace(route)
                ? route
                : null;
    }

    public void SetDirty(object source)
    {
        ArgumentNullException.ThrowIfNull(source);

        Take(source, inherited: false);
    }

    // Inside a submit's own window (BeginSave) the report is held rather than taken: the handler
    // navigates from in there, so a report taken here would have the exit guard protest a departure
    // the save itself caused (nsail#1450). Held, not dropped — the window answers for it at EndSave.
    void Take(object source, bool inherited)
    {
        var raiseChanged = false;

        lock (_sync)
        {
            if (_retired.TryGetValue(source, out _))
            {
                return;
            }

            if (Hold(source, inherited))
            {
                return;
            }

            if (_dirtySources.Add(source) && _dirtySources.Count == 1)
            {
                _hasChanges = true;
                raiseChanged = true;
            }
        }

        if (raiseChanged)
        {
            StateChanged?.Invoke();
        }
    }

    // A source's end: its report is spent and no later one is taken. A component that unmounts
    // with a gesture still in flight (a repricing read, a fitting read) reports itself when the
    // read comes back, after its page is gone; that report has no page left to spend it and the
    // surface outlives the page, so every later screen on it would ask about changes nobody made.
    public void Retire(object source)
    {
        ArgumentNullException.ThrowIfNull(source);

        lock (_sync)
        {
            _retired.AddOrUpdate(source, source);
        }

        SetUnchanged(source);
        ReleaseRefusal(source);
    }

    // ONE form answers for the surface, and it is the first to report: a surface can hold a second —
    // a filter beside a document — while the way out is single, so "whichever of them refuses" would
    // have a filter's own search grey the X over a Cancelar that stayed live, which is the two faces
    // of the exit disagreeing again (ui/actions.md). The first to report is the first the host
    // mounted: the outer form of a nested pair, the first of two siblings, and on a dialog the one
    // thing it was opened to ask. Reported after the render that decided it (NsForm), so this is
    // what that form is cascading and never a state it has not drawn.
    internal void SetRefusing(object source, bool disabled, bool running)
    {
        ArgumentNullException.ThrowIfNull(source);

        var raiseChanged = false;

        lock (_sync)
        {
            _refusingSource ??= source;

            if (!ReferenceEquals(_refusingSource, source)
                || (_formDisabled == disabled && _formRunning == running))
            {
                return;
            }

            _formDisabled = disabled;
            _formRunning = running;
            raiseChanged = true;
        }

        if (raiseChanged)
        {
            RefusalChanged?.Invoke();
        }
    }

    // A form's end hands the answer back, so the next one mounted on this surface can claim it. The
    // chrome that reads it outlives the form — the main surface outlives every page on it — so a
    // claim left standing would grey a later screen's way out with a refusal nobody is left to lift.
    void ReleaseRefusal(object source)
    {
        var raiseChanged = false;

        lock (_sync)
        {
            if (!ReferenceEquals(_refusingSource, source))
            {
                return;
            }

            _refusingSource = null;
            raiseChanged = _formDisabled || _formRunning;
            _formDisabled = false;
            _formRunning = false;
        }

        if (raiseChanged)
        {
            RefusalChanged?.Invoke();
        }
    }

    // ONE title row stands in for the vanished app bar, and it is the first one this surface
    // rendered: the way back to a hidden drawer is the surface's and a screen offers it once, so a
    // panel that draws a second title row gets no second hamburger (ui/surfaces.md, the announce
    // seam). Asked from the row's render and not once at its creation, because the claim changes
    // hands while the surface lives — a step that replaces its panel initializes the arriving row
    // before it disposes the leaving one, so the only moment that can be trusted is the render
    // after the release.
    internal bool ClaimsToggle(object source)
    {
        ArgumentNullException.ThrowIfNull(source);

        lock (_sync)
        {
            _toggleSource ??= source;

            return ReferenceEquals(_toggleSource, source);
        }
    }

    // A row's end hands the toggle back, so the next one on this surface can take it. The main
    // surface outlives every page on it, so a claim left standing is a phone with no way to the
    // drawer on every screen reached after this one.
    internal void ReleaseToggle(object source)
    {
        var released = false;

        lock (_sync)
        {
            if (ReferenceEquals(_toggleSource, source))
            {
                _toggleSource = null;
                released = true;
            }
        }

        if (released)
        {
            StateChanged?.Invoke();
        }
    }

    public void SetUnchanged(object source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var raiseChanged = false;

        lock (_sync)
        {
            // Out of every open window besides: a source that spends its own report — an editor's
            // ClearDirty, the Retire an unmount goes through — is owed nothing back by a submit
            // that ends refused.
            foreach (var window in _windows)
            {
                window.Echoes.Remove(source);
                window.Inherited.Remove(source);
            }

            if (_dirtySources.Remove(source) && _dirtySources.Count == 0)
            {
                _hasChanges = false;
                raiseChanged = true;
            }
        }

        if (raiseChanged)
        {
            StateChanged?.Invoke();
        }
    }

    // Spends the reports the given document's save covers: its own, and the EDITORS' — the
    // collection editors whose values no EditContext sees, which is why each of them reports the
    // surface itself and why those values are part of that very document (NsForm.HandleSubmit).
    // Not every report: another form is a document of its own (IFormDocument) and a surface can
    // hold two, so the other one's stands. What was spent is returned because a refused submit
    // owes it back — an editor's report is the only record of values nothing here can re-derive.
    internal IReadOnlyCollection<object> ClearChanges(object document)
    {
        ArgumentNullException.ThrowIfNull(document);

        List<object> spent = [];
        var raiseChanged = false;

        lock (_sync)
        {
            foreach (var source in _dirtySources)
            {
                if (Spendable(source, document))
                {
                    spent.Add(source);
                }
            }

            foreach (var source in spent)
            {
                _dirtySources.Remove(source);
            }

            if (_hasChanges && _dirtySources.Count == 0)
            {
                _hasChanges = false;
                raiseChanged = true;
            }
        }

        if (raiseChanged)
        {
            StateChanged?.Invoke();
        }

        return spent;
    }

    // Opens the window this submit runs inside: a report filed while it is open is HELD rather than
    // taken (SetDirty), and the submit answers for it at EndSave. The window is handed to the CALL
    // and never looked up by document — two submits on one document overlap (nothing disables the
    // plain button beside a Guardar, and the second one unwinds through a finally of its own), so
    // each closes only the window it opened and reads nothing another one is holding.
    internal SaveWindow BeginSave(object document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var window = new SaveWindow(document);

        lock (_sync)
        {
            _windows.Add(window);
        }

        return window;
    }

    // Closes the caller's own window and answers with what was filed inside it, for the submit that
    // owes it back when it ends refused (NsForm.HandleSubmit). A success drops that: it is the echo
    // of values the save took. What the window INHERITED is nobody's echo — another submit's refusal
    // handed it in — so it goes back to the surface here whatever this submit's outcome.
    internal IReadOnlyCollection<object> EndSave(SaveWindow? window)
    {
        if (window is null)
        {
            return [];
        }

        IReadOnlyCollection<object> echoes;
        IReadOnlyCollection<object> inherited;

        lock (_sync)
        {
            if (!_windows.Remove(window))
            {
                return [];
            }

            echoes = [.. window.Echoes];
            inherited = [.. window.Inherited];
        }

        PutBack(inherited);

        return echoes;
    }

    // Hands reports back for a submit that ends refused: what its spend took and what its window
    // held. Not SetDirty — a window still open over them is about to navigate out of a save of its
    // own, so it inherits them rather than the surface taking them (nsail#1450), and owes them back
    // at its own close because it neither spent nor caused them.
    internal void PutBack(IEnumerable<object> reports)
    {
        ArgumentNullException.ThrowIfNull(reports);

        foreach (var report in reports)
        {
            Take(report, inherited: true);
        }
    }

    // Whether an open window answers for this report instead of the surface taking it. EVERY window
    // that covers the source holds it: an editor names no document — which is why the spend reaches
    // it for whichever form is saving — so every submit in flight answers for that report, each
    // with the answer its own outcome gives. Called under _sync.
    bool Hold(object source, bool inherited)
    {
        var held = false;

        foreach (var window in _windows)
        {
            if (!Spendable(source, window.Document))
            {
                continue;
            }

            if (inherited)
            {
                window.Inherited.Add(source);
            }
            else
            {
                window.Echoes.Add(source);
            }

            held = true;
        }

        return held;
    }

    // The bound both halves read by, and it reaches exactly this far: another FORM's report is its
    // own document's business, spent and held by nobody else. Anything else reporting the surface
    // belongs to whichever document is saving — no editor names the one it speaks for — so an
    // editor's report is in reach of every submit on the surface, not only its own page's.
    static bool Spendable(object source, object document)
    {
        return source is not IFormDocument || ReferenceEquals(source, document);
    }

    public async Task Report(ProblemEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var handler = OnProblem;

        if (handler is not null)
        {
            await handler(args);
        }
    }

    /// <summary>The form an act the page hosts is standing INSIDE, for the length of that act's
    /// call. The cascade is what decides which form — an act drawn inside one is that form's
    /// business and an act beside it is not — while the Runner that raises the act belongs to the
    /// page, so its refusal arrives with no way back to the form the person pressed it in: the
    /// surface is the one object both ends hold, the way it already is for the word a dialog's X
    /// reads (SetRefusing). Held for the length of the call and nothing longer — the surface
    /// outlives every form on it, and a claim left standing would hand a later screen's refusal to
    /// a form nobody can see.</summary>
    internal ActClaim BeginAct(IFormProblems form)
    {
        ArgumentNullException.ThrowIfNull(form);

        // The last "no" this seam placed comes down here and not at the end of the act that
        // replaced it: an act that is refused reports a Problem and one that works reports
        // nothing, so silence is only ever legible backwards, at the next press.
        form.Place(null);

        var claim = new ActClaim(form);

        lock (_sync)
        {
            _acts.Add(claim);
        }

        return claim;
    }

    internal void EndAct(ActClaim? claim)
    {
        if (claim is null)
        {
            return;
        }

        lock (_sync)
        {
            _acts.Remove(claim);
        }
    }

    /// <summary>Which form answers for an act in flight: the last claim taken, the way a nested
    /// cascade would answer. Two acts overlap on one surface only across two pages' Runners — one
    /// page runs one act at a time — and the later press is the one the person is waiting on.</summary>
    internal IFormProblems? HostedAct
    {
        get
        {
            lock (_sync)
            {
                return _acts.Count == 0 ? null : _acts[^1].Form;
            }
        }
    }

    // One hosted act's claim, identified by nothing but itself: the caller holds it from BeginAct
    // to EndAct, which is what keeps a second act on the same surface from closing it.
    internal sealed class ActClaim(IFormProblems form)
    {
        internal IFormProblems Form { get; } = form;
    }

    // One submit's window, identified by nothing but itself: the caller holds it from BeginSave to
    // EndSave, which is what keeps a second submit on the same document out of it.
    internal sealed class SaveWindow(object document)
    {
        internal object Document { get; } = document;

        // Filed inside the window: this save's own echo, which a save that took drops.
        internal HashSet<object> Echoes { get; } = [];

        // Handed in by another submit's refusal: unsaved work this save neither spent nor caused,
        // so its own success does not drop it.
        internal HashSet<object> Inherited { get; } = [];
    }

}
