// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.ComponentModel.DataAnnotations;

namespace NSail.Sample.Contacts.Models;

public class ContactAddressModel
{
    public Guid Id { get; set; }

    public AddressKind Kind { get; set; }

    [Required]
    [MaxLength(200)]
    public string Street { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? City { get; set; }
}
