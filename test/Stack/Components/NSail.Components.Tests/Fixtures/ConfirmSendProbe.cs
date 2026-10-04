// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Messaging;

namespace NSail.Components.Tests.Fixtures;

/// <summary>Renders nothing and exists to reach NsPartial.ConfirmSend from a test — the door
/// every grid's delete goes through, asked directly instead of through one page's markup.</summary>
public sealed class ConfirmSendProbe : NsPartial
{
    public Task<bool> Run(IMessage message)
    {
        return ConfirmSend(message);
    }
}

/// <summary>Its full name resolves to Area "Components", Object "DeleteProbeThing" under the
/// default metadata template, so its derived keys are "Components.DeleteProbeThing.*".</summary>
public sealed class DeleteProbeThing : IMessage
{
    public Guid Id { get; set; }
}
