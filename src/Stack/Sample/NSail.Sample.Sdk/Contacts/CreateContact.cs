// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.ComponentModel.DataAnnotations;
using NSail.Messaging;

namespace NSail.Sample.Contacts;

[Http(Post, "api/sample/contacts")]
public class CreateContact : IMessage
{
    public required Guid Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public ContactKind Kind { get; set; }

    [EmailAddress]
    [MaxLength(200)]
    public string? Email { get; set; }

    [MaxLength(50)]
    public string? Phone { get; set; }

    public DateOnly? BirthDate { get; set; }

    [Range(0, 100)]
    public int Rating { get; set; }

    public bool IsFavorite { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}
