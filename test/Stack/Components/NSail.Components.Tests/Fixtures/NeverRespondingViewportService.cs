// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using MudBlazor;
using MudBlazor.Services;

namespace NSail.Components.Tests.Fixtures;

/// <summary>Stands in for the gap FixedBreakpointViewportService models the far side of: the
/// window before the JS-backed viewport service has reported in at all — SSR, or a slow WASM
/// attach on a phone (subscribe never resolves, matching a client that never gets there in
/// the render this test captures). NsDrawer's own comment names the consequence directly:
/// "Assume a wide viewport until measured", so a docked-mode aside opens believing itself
/// docked (Breakpoint.Lg falls inside IsDocked's Md..Xxl band) regardless of what the real
/// device is. GetCurrentBreakpointAsync still answers Lg for any caller that polls instead of
/// subscribing, so nothing here ever contradicts the "wide until measured" assumption.</summary>
public sealed class NeverRespondingViewportService : IBrowserViewportService
{
    public ResizeOptions ResizeOptions { get; set; } = new();

    public Task SubscribeAsync(Guid subscriptionId, Action<BrowserViewportEventArgs> onResized, ResizeOptions? options = null, bool fireImmediately = true)
    {
        return Task.CompletedTask;
    }

    public Task SubscribeAsync(Guid subscriptionId, Func<BrowserViewportEventArgs, Task> onResized, ResizeOptions? options = null, bool fireImmediately = true)
    {
        return Task.CompletedTask;
    }

    public Task SubscribeAsync(IBrowserViewportObserver observer, bool fireImmediately = true)
    {
        return Task.CompletedTask;
    }

    public Task<bool> IsMediaQueryMatchAsync(string mediaQuery)
    {
        return Task.FromResult(false);
    }

    public Task<bool> IsBreakpointWithinWindowSizeAsync(Breakpoint breakpointToMatch)
    {
        return Task.FromResult(false);
    }

    public Task<bool> IsBreakpointWithinReferenceSizeAsync(Breakpoint breakpointToMatch, Breakpoint referenceBreakpoint)
    {
        return Task.FromResult(false);
    }

    public Task<Breakpoint> GetCurrentBreakpointAsync()
    {
        return Task.FromResult(Breakpoint.Lg);
    }

    public Task<BrowserWindowSize> GetCurrentBrowserWindowSizeAsync()
    {
        return Task.FromResult(new BrowserWindowSize { Width = 1920, Height = 1080 });
    }

    public Task UnsubscribeAsync(Guid subscriptionId)
    {
        return Task.CompletedTask;
    }

    public Task UnsubscribeAsync(IBrowserViewportObserver observer)
    {
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
