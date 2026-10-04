// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using NSail.Messaging;

namespace NSail.Sample.Contacts;

[Http(Delete, "api/sample/contacts/{id}")]
public class DeleteContact : IMessage
{
    [AsRoute]
    public required Guid Id { get; set; }
}
