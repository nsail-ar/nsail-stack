// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Messaging;

/// <summary>Marks a Send message that produces a result. Errors flow as thrown BusinessExceptions, not result envelopes.</summary>
public interface IMessage<TResult>
{
}

/// <summary>Marks a Send command (no result) or a Publish event.</summary>
public interface IMessage
{
}
