// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using NSail.Messaging;

namespace NSail.Sample.Contacts;

public class ContactSaved : ISaved
{
    public required Guid Id { get; init; }
}
