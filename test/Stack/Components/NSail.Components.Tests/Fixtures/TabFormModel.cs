// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.ComponentModel.DataAnnotations;

namespace NSail.Components.Tests.Fixtures;

public sealed class TabFormModel
{
    public string? First { get; set; }

    [Required]
    public string? Second { get; set; }
}
