// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Data;
using NSail.Mapping.Annotations;
using NSail.Sample.Contacts;
using NSail.Sample.Contacts.Models;

namespace NSail.Sample.Entities;

[MapFrom(typeof(CreateContact))]
[MapFrom(typeof(UpdateContact))]
[MapTo(typeof(ContactRow))]
[MapTo(typeof(ContactModel))]
public class Contact : IVersioned, IAudited
{
    public Guid Id { get; set; }

    public uint Version { get; set; }

    public string Name { get; set; } = string.Empty;

    public ContactKind Kind { get; set; }

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public DateOnly? BirthDate { get; set; }

    public int Rating { get; set; }

    public bool IsFavorite { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    [Composition]
    public List<ContactAddress> Addresses { get; set; } = [];
}
