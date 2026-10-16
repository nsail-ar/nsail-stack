// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.ComponentModel.DataAnnotations;
using NSail.Messaging.Runtime.Validation;

namespace NSail.Testing.Models;

public sealed class TestModel
{
    public string? Name { get; set; }

    public int Age { get; set; }
}

public sealed class CollectionProblemModel
{
    [Validated]
    public List<AddressRow> Addresses { get; set; } = [];
}

public sealed class AddressRow
{
    [Required]
    public string? Street { get; set; }
}
