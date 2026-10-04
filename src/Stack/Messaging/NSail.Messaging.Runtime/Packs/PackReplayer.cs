// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Text.Json;
using NSail.Problems;
using NSail.Serialization;

namespace NSail.Messaging.Runtime.Packs;

/// <summary>Sends a pack's steps in order, resolving each name through the registry. It
/// knows nothing about what a pack contains — no roles, no products, no organization — and
/// it does not find the file either: whoever owns the pack hands it over.</summary>
public class PackReplayer
{
    readonly Mediator _mediator;
    readonly MessageRegistry _registry;

    public PackReplayer(Mediator mediator, MessageRegistry registry)
    {
        _mediator = mediator;
        _registry = registry;
    }

    public async Task<PackReplay> Replay(Pack pack, PackFailure onFailure, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pack);

        var replay = new PackReplay();

        for (var index = 0; index < pack.Steps.Count; index++)
        {
            var step = pack.Steps[index];

            try
            {
                await _mediator.Send(Materialize(step), cancellationToken).ConfigureAwait(false);

                replay.Imported++;
            }
            catch (BusinessException exception) when (exception.Code == "AlreadyExists")
            {
                // A replay is meant to be run again: a step whose subject is already there is
                // done, not refused. This is what makes a retry after a partial run resume.
                replay.Skipped++;
            }
            catch (Exception exception)
            {
                replay.Errors.Add(new PackStepError { Index = index, Step = step, Detail = Describe(exception) });

                if (onFailure == PackFailure.Stop)
                {
                    break;
                }
            }
        }

        return replay;
    }

    /// <summary>The step as the message it names, without sending it — for an owner that
    /// answers a whole pack in one pass instead of step by step, and still wants the one
    /// reading of a step the replay itself uses.</summary>
    public IMessage Materialize(PackStep step)
    {
        ArgumentNullException.ThrowIfNull(step);

        var messageType = _registry.Resolve(step.Message)
            ?? throw new InvalidOperationException($"No message is registered for '{step.Message}' — the pack is stale against the message registry.");

        var instance = JsonSerializer.Deserialize(step.Body.GetRawText(), messageType, JsonOptions.Wire);

        return instance as IMessage
            ?? throw new InvalidOperationException($"'{step.Message}' does not deserialize to a Send-able IMessage (no result messages are not supported by this replay).");
    }

    /// <summary>What a failed step says — the refusing rule's own words where the failure names
    /// any. Public for an owner that catches its own steps instead of letting a replay collect
    /// them, so one pack's error reads like every other's.</summary>
    public static string Describe(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception is BusinessException business
            ? business.Issues.Count > 0 ? business.Issues[0].Message : business.Title
            : exception.Message;
    }
}
