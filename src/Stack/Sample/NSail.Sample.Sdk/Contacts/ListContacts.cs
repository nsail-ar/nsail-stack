// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Messaging;
using NSail.Messaging.Runtime.Validation;
using NSail.Paging;
using NSail.Sample.Contacts.Models;

namespace NSail.Sample.Contacts;

[Http(Get, "api/sample/contacts")]
public class ListContacts : PagedMessage, IMessage<DataPage<ContactRow>>
{
    [SearchTerm]
    public string? Search { get; set; }

    public ContactKind? Kind { get; set; }
}
