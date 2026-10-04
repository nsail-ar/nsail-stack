// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Security.Annotations;

/// <summary>Declares a message this one depends on (a create needs its lookup): whoever
/// may send the primary may also send the required message. One hop, flat implication —
/// only lookup-shaped messages (safe by response) belong here.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class RequiresAttribute : Attribute
{
    public RequiresAttribute(Type message)
    {
        Message = message;
    }

    public Type Message { get; }
}
