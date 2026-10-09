// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.Logging;

namespace NSail.Messaging.Runtime.Publishing;

/// <summary>What a client's feed does with what comes down the line, and what it says when the
/// line comes up. Every transport's <see cref="PushFeed"/> holds one, so the rules a screen
/// depends on — which names are published, that a connect is a catch-up, that a listener's
/// failure is said out loud — are one rule and not one per transport.</summary>
public sealed class PushDispatch
{
    readonly Mediator _mediator;
    readonly ILogger _logger;
    readonly Dictionary<string, PushedMessage> _known;

    /// <summary><paramref name="known"/> is what the composition's Push.Clients targets
    /// registered, one entry per <c>[Pushed]</c> message of every kit this client mounts.</summary>
    public PushDispatch(Mediator mediator, IEnumerable<PushedMessage> known, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(known);

        _mediator = mediator;
        _logger = logger;
        _known = known.ToDictionary(pushed => pushed.Name, StringComparer.Ordinal);
    }

    /// <summary>What arrived, published here as if it had been raised on this side. A name this
    /// composition did not register is dropped: the server's word picks an entry from the closed
    /// list, never which type a string deserializes into.</summary>
    public async Task Receive(string name, string body)
    {
        if (!_known.TryGetValue(name, out var pushed))
        {
            return;
        }

        try
        {
            await pushed.Publish(_mediator, body, CancellationToken.None);
        }
        catch (Exception failure)
        {
            // The transport's own handler would swallow it, and a listener that failed to re-read
            // is exactly the stale screen the push exists to prevent: said, never silent.
            _logger.LogError(failure, "A listener of the pushed event {Name} failed.", name);
        }
    }

    /// <summary>The line is up — for the first time, or again after a drop. Nothing is replayed,
    /// so this is the one promise a listener's catch-up rests on.</summary>
    public async Task Connected()
    {
        try
        {
            await _mediator.Publish(new PushConnected());
        }
        catch (Exception failure)
        {
            _logger.LogError(failure, "A listener failed on the push connecting; its catch-up read did not happen.");
        }
    }
}
