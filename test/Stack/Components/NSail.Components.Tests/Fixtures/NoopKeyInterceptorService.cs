// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Microsoft.AspNetCore.Components.Web;
using MudBlazor;
using MudBlazor.Services;

namespace NSail.Components.Tests.Fixtures;

/// <summary>MudBlazor's own KeyInterceptorService only implements IAsyncDisposable, which
/// bUnit's synchronous test-class teardown cannot dispose — this stub, registered last so
/// DI resolves it in its place, keeps rendering working without a real JS interop keyboard
/// bridge (none of these tests exercise keyboard shortcuts).</summary>
sealed class NoopKeyInterceptorService : IKeyInterceptorService, IDisposable
{
    public Task SubscribeAsync(IKeyInterceptorObserver observer, KeyInterceptorOptions options)
    {
        return Task.CompletedTask;
    }

    public Task SubscribeAsync(string elementId, KeyInterceptorOptions options, Action<KeyMapBuilder> configure)
    {
        return Task.CompletedTask;
    }

    public Task SubscribeAsync(string elementId, KeyInterceptorOptions options, IKeyDownObserver? keyDown, IKeyUpObserver? keyUp)
    {
        return Task.CompletedTask;
    }

    public Task SubscribeAsync(string elementId, KeyInterceptorOptions options, Action<KeyboardEventArgs>? keyDown, Action<KeyboardEventArgs>? keyUp)
    {
        return Task.CompletedTask;
    }

    public Task SubscribeAsync(string elementId, KeyInterceptorOptions options, Func<KeyboardEventArgs, Task>? keyDown, Func<KeyboardEventArgs, Task>? keyUp)
    {
        return Task.CompletedTask;
    }

    public Task DispatchAsync(string elementId, KeyEventKind kind, KeyboardEventArgs args)
    {
        return Task.CompletedTask;
    }

    public Task UpdateKeyAsync(IKeyInterceptorObserver observer, KeyOptions options)
    {
        return Task.CompletedTask;
    }

    public Task UpdateKeyAsync(string elementId, KeyOptions options)
    {
        return Task.CompletedTask;
    }

    public Task UnsubscribeAsync(IKeyInterceptorObserver observer)
    {
        return Task.CompletedTask;
    }

    public Task UnsubscribeAsync(string elementId)
    {
        return Task.CompletedTask;
    }

    public void Dispose()
    {
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
