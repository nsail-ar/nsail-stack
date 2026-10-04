// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using System.Net;

namespace NSail.Components.Tests.Fixtures;

/// <summary>The app's own static assets, answered without a server: a path the map carries is
/// its text, anything else a 404 — which is what a chapter no module translated looks like.
/// Every address asked for is recorded, so a test can prove that opening one chapter fetched
/// one file and no other.</summary>
public sealed class StaticAssets(IReadOnlyDictionary<string, string> files) : HttpMessageHandler
{
    readonly List<string> _requested = [];

    TaskCompletionSource? _held;

    public IReadOnlyList<string> Requested => _requested;

    /// <summary>Holds every answer from here on until Release, so a test can look at the screen
    /// while a chapter is still in flight — which is the whole window a page can show the wrong
    /// chapter in, and it is instantaneous against a handler that answers at once.</summary>
    public void Hold()
    {
        _held = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public void Release()
    {
        var held = _held;

        _held = null;

        held?.SetResult();
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var path = request.RequestUri!.AbsolutePath.TrimStart('/');

        _requested.Add(path);

        if (_held is { } held)
        {
            await held.Task.WaitAsync(cancellationToken);
        }

        if (!files.TryGetValue(path, out var content))
        {
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(content),
        };
    }
}
