// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.ComponentModel.DataAnnotations;

namespace NSail.Sample;

public class SampleOptions
{
    [Required]
    public string ConnectionString { get; set; } = string.Empty;
}
