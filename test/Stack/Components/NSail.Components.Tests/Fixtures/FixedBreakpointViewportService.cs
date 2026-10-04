// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using MudBlazor;
using MudBlazor.Services;

namespace NSail.Components.Tests.Fixtures;

/// <summary>Stands in for MudBlazor's real IBrowserViewportService, which resolves the
/// browser's width through JS interop — bUnit's loose JSInterop mode answers every call
/// with default(TValue), which is not the "assume Lg" NsDrawer.razor documents for its own
/// first render (that assumption models a pending-measurement gap, not a stuck-at-default
/// browser). This fake reports a fixed Breakpoint synchronously on subscribe, so a docked
/// vs. overlay test can force the branch it needs deterministically instead of depending on
/// what a mocked JS runtime happens to return.</summary>
public sealed class FixedBreakpointViewportService(Breakpoint breakpoint) : IBrowserViewportService
{
    public ResizeOptions ResizeOptions { get; set; } = new();

    public Task SubscribeAsync(Guid subscriptionId, Action<BrowserViewportEventArgs> onResized, ResizeOptions? options = null, bool fireImmediately = true)
    {
        if (fireImmediately)
        {
            onResized(new BrowserViewportEventArgs(subscriptionId, new BrowserWindowSize { Width = 1920, Height = 1080 }, breakpoint));
        }

        return Task.CompletedTask;
    }

    public Task SubscribeAsync(Guid subscriptionId, Func<BrowserViewportEventArgs, Task> onResized, ResizeOptions? options = null, bool fireImmediately = true)
    {
        if (fireImmediately)
        {
            return onResized(new BrowserViewportEventArgs(subscriptionId, new BrowserWindowSize { Width = 1920, Height = 1080 }, breakpoint));
        }

        return Task.CompletedTask;
    }

    public Task SubscribeAsync(IBrowserViewportObserver observer, bool fireImmediately = true)
    {
        if (fireImmediately)
        {
            return observer.NotifyBrowserViewportChangeAsync(new BrowserViewportEventArgs(observer.Id, new BrowserWindowSize { Width = 1920, Height = 1080 }, breakpoint));
        }

        return Task.CompletedTask;
    }

    public Task<bool> IsMediaQueryMatchAsync(string mediaQuery)
    {
        return Task.FromResult(false);
    }

    public Task<bool> IsBreakpointWithinWindowSizeAsync(Breakpoint breakpointToMatch)
    {
        return Task.FromResult(breakpointToMatch == breakpoint);
    }

    public Task<bool> IsBreakpointWithinReferenceSizeAsync(Breakpoint breakpointToMatch, Breakpoint referenceBreakpoint)
    {
        return Task.FromResult(breakpointToMatch == referenceBreakpoint);
    }

    public Task<Breakpoint> GetCurrentBreakpointAsync()
    {
        return Task.FromResult(breakpoint);
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
