// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using NSail.Messaging.Runtime.Context;

namespace NSail.Components;

// Deny by default: every page requires a signed-in user unless it opts out with
// [AllowAnonymous] (sign-in, sign-out) — the same override AuthorizeRouteView already
// honors for MVC-style [Authorize] class + [AllowAnonymous] action. One place instead of
// stamping [Authorize] on every page.
[Authorize]
public abstract class NsPage : NsPartial
{
    protected bool HasWork => Surface?.HasWork ?? false;

    protected bool HasChanges => Surface?.HasChanges ?? false;

    [Inject]
    protected ProblemManager? Problems { get; init; }

    [Inject]
    protected NavigationManager Navigation { get; init; } = default!;

    // Read live off the query, never copied into a field at initialization: the same rule
    // every other query value on this page follows, and the one that keeps a page reused
    // across two addresses from publishing the token of the first one.
    protected override string? Source
    {
        get { return GetQuery<string>(MessageHeaders.Source); }
    }

    protected sealed override void OnInitialized()
    {
        base.OnInitialized();

        Navigation.LocationChanged += HandleLocationChanged;

        if (Surface is not null)
        {
            Surface.StateChanged += OnSurfaceStateChanged;
            Surface.OnProblem += OnSurfaceProblem;
            Surface.ResetView();
        }

        OnCreated();
    }

    // Through a Runner, never bare: an OnCreatedAsync that threw reached the layout's
    // NsErrorBoundary and replaced the whole app with the error splash, so a database that
    // was down cost the user every pixel instead of the one list that could not be read. A
    // region that draws its own failure (NsLoad) never gets here; this is the floor under
    // everything that still loads from the page itself.
    //
    // A Runner OF ITS OWN, not the page's: the page's is the one every click runs on, and a
    // Run that starts while another is in flight throws. The load holds a Runner for as long
    // as the read takes, which is exactly when a click lands — a row action pressed on a slow
    // backend would fault out of the click and into the splash this floor exists to close.
    // Replace is the other way to silence that, and the wrong one: the click would cancel the
    // read it arrived during.
    protected sealed override Task OnInitializedAsync()
    {
        return Using(new Runner(this)).Run(OnCreatedAsync);
    }

    /// <summary>Page initialization: the surface is available and the base wiring is done. Runs before the first render.</summary>
    protected virtual void OnCreated()
    {
    }

    protected virtual Task OnCreatedAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>Declares this page's content size on its surface (see SurfaceContext.Size).</summary>
    protected void SetSize(NsSize size)
    {
        Surface?.SetSize(size);
    }

    protected void SetFloating(bool floating)
    {
        Surface?.SetFloating(floating);
    }

    /// <summary>Navigates within this page's surface (full-page on the default surface, in place on a named one).</summary>
    protected void Navigate(string route)
    {
        if (Surface is not null)
        {
            Surface.Navigate(route);
            return;
        }

        Navigation.NavigateTo(route.StartsWith('/') ? route : "/" + route);
    }

    /// <summary>Typed form of Navigate(route) — GetUrl&lt;TPage&gt; resolution built in.</summary>
    protected void Navigate<TPage>(object? parameters = null) where TPage : IComponent
    {
        if (Surface is not null)
        {
            Surface.Navigate<TPage>(parameters);
            return;
        }

        Navigation.NavigateTo(GetUrl<TPage>(parameters));
    }

    /// <summary>Opens a route in a named surface (e.g. "aside", "modal").</summary>
    protected void Navigate(Surface surface, string route)
    {
        if (Surface is not null)
        {
            Surface.Open(surface, route);
            return;
        }

        // No cascade to ask, and so nowhere to record that this open pushed: the close would
        // find nothing recorded, rewrite the address and leave the pushed entry standing above
        // it. Replacing is the half of the rule that stays true to itself with no surface to
        // ask — the same close a pasted address gets.
        Navigation.NavigateTo(
            Navigation.GetUriWithQueryParameter(surface.Name, route.TrimStart('/')),
            SurfaceContext.InPlace);
    }

    /// <summary>A query value carried by the route THIS page was opened through — the browser's
    /// own query on the main surface, the inner ?aside=/?modal= route's inside an overlay, which
    /// is where a hosted page's query actually travels. Use it instead of
    /// [SupplyParameterFromQuery], which binds from the real URI and so reads null for every
    /// page an overlay hosts.
    ///
    /// Nothing is cached: read it wherever the page applies it, and read it again from
    /// OnParametersSet if a changed query has to land without a remount (the main surface reuses
    /// the component between two addresses that resolve to the same page; a named surface is
    /// keyed by its inner route and remounts on its own).</summary>
    protected T? GetQuery<T>(string name)
    {
        if (Surface is not null)
        {
            return Surface.GetQuery<T>(name);
        }

        // No cascade to ask, and a page nothing hosts is on the address bar by construction —
        // the same answer the main surface would give.
        return SurfaceQuery.GetValue<T>(Navigation.ToAbsoluteUri(Navigation.Uri).Query, name);
    }

    /// <summary>Writes a query value onto the route THIS page was opened through — the write
    /// half of GetQuery, and the one door for it. The browser's own query on the main surface,
    /// the inner ?aside=/?modal= route's inside an overlay, rewritten in place so nothing nests;
    /// on a dialog, which is routed by nothing and carries no query, it is a no-op.
    ///
    /// A null value removes the parameter, and the write always REPLACES the history entry: a
    /// tab or a filter is a view of the place the user is at, not a place of its own. The value
    /// is formatted the way GetQuery parses it, so a round trip gives back what was written.
    ///
    /// The write lands as a navigation, so apply the query where it is READ — in
    /// OnParametersSet, never copied into a field at initialization.</summary>
    protected void SetQuery(string name, object? value)
    {
        SetQuery(new Dictionary<string, object?> { [name] = value });
    }

    /// <summary>The batched form of SetQuery(name, value) — see SurfaceContext's own, which
    /// this forwards to when a surface is hosting the page.</summary>
    protected void SetQuery(IReadOnlyDictionary<string, object?> values)
    {
        if (Surface is not null)
        {
            Surface.SetQuery(values);
            return;
        }

        // No cascade to ask, and a page nothing hosts is on the address bar by construction —
        // the same write the main surface would make.
        var uri = Navigation.ToAbsoluteUri(Navigation.Uri);
        var query = uri.Query;

        foreach (var (name, value) in values)
        {
            query = SurfaceQuery.SetValue(query, name, value);
        }

        Navigation.NavigateTo(uri.GetLeftPart(UriPartial.Path) + query, SurfaceContext.InPlace);
    }

    void HandleLocationChanged(object? sender, LocationChangedEventArgs args)
    {
        _ = InvokeAsync(() => OnLocationChanged(args));
    }

    protected virtual Task OnLocationChanged(LocationChangedEventArgs args)
    {
        return Task.CompletedTask;
    }

    protected virtual void OnProblemReported(ProblemEventArgs args)
    {
    }

    protected virtual Task OnProblemReportedAsync(ProblemEventArgs args)
    {
        return Task.CompletedTask;
    }

    protected virtual void OnSurfaceStateChanged()
    {
        StateHasChanged();
    }

    protected virtual async Task OnSurfaceProblem(ProblemEventArgs args)
    {
        OnProblemReported(args);
        await OnProblemReportedAsync(args);

        if (!args.Handled && Problems is not null)
        {
            await Problems.Report(args);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Navigation.LocationChanged -= HandleLocationChanged;

            if (Surface is not null)
            {
                Surface.StateChanged -= OnSurfaceStateChanged;
                Surface.OnProblem -= OnSurfaceProblem;
            }
        }

        base.Dispose(disposing);
    }
}
