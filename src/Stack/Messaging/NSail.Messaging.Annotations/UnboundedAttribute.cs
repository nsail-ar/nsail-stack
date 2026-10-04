// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.Messaging.Annotations;

/// <summary>Records that a numeric or code member declares no bound of its own, and why —
/// either because the domain names none (a concurrency token is not a quantity) or because the
/// bound is about a value the message does not carry, and a rule beside it owns the whole of it.
/// It refuses nothing at runtime: DeclaredBoundTests (NSail.Architecture.Tests) is what reads
/// it, and the attribute exists so the answer is a decision on the record instead of a silence.
/// The reason is the record — one that says "no bound" says nothing the gate did not already
/// know.</summary>
[AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
public sealed class UnboundedAttribute : Attribute
{
    public UnboundedAttribute(string reason)
    {
        Reason = reason;
    }

    public string Reason { get; }
}
