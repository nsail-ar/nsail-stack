// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Mapping.Annotations;
using NSail.Sample.Contacts;
using NSail.Sample.Contacts.Models;

namespace NSail.Sample.Entities;

[MapFrom(typeof(ContactAddressModel))]
[MapTo(typeof(ContactAddressModel))]
public class ContactAddress
{
    public Guid Id { get; set; }

    public Guid ContactId { get; set; }

    public AddressKind Kind { get; set; }

    public string Street { get; set; } = string.Empty;

    public string? City { get; set; }

    [Parent]
    public Contact? Contact { get; set; }
}
