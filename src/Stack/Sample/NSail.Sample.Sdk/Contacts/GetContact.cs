// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using NSail.Messaging;
using NSail.Sample.Contacts.Models;

namespace NSail.Sample.Contacts;

[Http(Get, "api/sample/contacts/{id}")]
public class GetContact : IMessage<ContactModel>
{
    [AsRoute]
    public required Guid Id { get; set; }
}
