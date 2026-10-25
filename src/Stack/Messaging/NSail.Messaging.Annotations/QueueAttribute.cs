// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Messaging.Annotations;

/// <summary>A send of this command is durable: it is stored as a row inside the sending
/// operation's own transaction and answers the caller the moment that transaction commits, and
/// the same <c>IHandler</c> as always runs it afterwards — in the tenant the send was made in,
/// in the install's default language, reading the headers the send carried. An operation that
/// rolls back never queues it; one that commits runs it even if the process dies immediately
/// after. A handler that keeps failing is retried with backoff and then dead-lettered.
///
/// <para>Only a command with no result (<c>IMessage</c>, never <c>IMessage&lt;T&gt;</c>): the
/// caller is told "accepted", never the outcome, and a screen that needs the outcome hears it
/// through a <c>[Pushed]</c> event. Declared INSTEAD of <c>[Http]</c>, never beside it — the
/// two would register two senders for one message. Both refusals are build errors (NSG014,
/// NSG015).</para>
///
/// <para>Read at compile time only: the kit's Queue.Producers and Queue.Consumers holders
/// register the two ends for every message carrying it.</para></summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class QueueAttribute : Attribute
{
}
