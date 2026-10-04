// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using NSail.Problems;

namespace NSail.Messaging.Runtime.Validation;

/// <summary>A save-time condition imposed on a message the implementer does not own — how a
/// composing app refuses a kit operation that breaks the app's rules. Returns the Problem that
/// refuses the send, or null to allow it, so a refusal reaches the screen through the same
/// channel as the handler's own BusinessException and nothing new travels on the wire.
/// The send pipeline runs validators innermost: after every interceptor, so a rule can never
/// answer before the security gate, and before the sender, so no handler ever sees a refused
/// message. Register one per message type with [Injectable(As = [typeof(IValidator&lt;T&gt;)])];
/// a rule that reads the database belongs to the host that owns the data, never to a client.</summary>
public interface IValidator<TMessage>
{
    Task<Problem?> Validate(TMessage message, CancellationToken cancellationToken);
}
