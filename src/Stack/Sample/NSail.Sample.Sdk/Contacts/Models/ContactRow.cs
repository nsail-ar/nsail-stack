// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.Sample.Contacts.Models;

public class ContactRow
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public ContactKind Kind { get; set; }

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public int Rating { get; set; }

    public bool IsFavorite { get; set; }

    public DateTime UpdatedAt { get; set; }
}
