// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Harness.Api;

public enum HarnessAccess { Control, Lease, Open }

// What a message's route asks of its caller. A message without it needs the control token.
[AttributeUsage(AttributeTargets.Class)]
public sealed class HarnessAccessAttribute(HarnessAccess access) : Attribute
{
    public HarnessAccess Access { get; } = access;
}
