// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components.Tests.Fixtures;

/// <summary>One member per text-shaped field in the house, for the suite that types into each
/// and submits in the same act — Nuevo Feriado's own shape (a required name and nothing else
/// touched) generalized to every box a value is typed into.</summary>
public sealed class TypedValueModel
{
    public string? Name { get; set; }

    public string? Document { get; set; }

    public string? Note { get; set; }

    public string? MailAddress { get; set; }

    public string? Phone { get; set; }

    public string? Site { get; set; }

    public string? Secret { get; set; }

    public string? Search { get; set; }

    public decimal? Amount { get; set; }

    public decimal? Price { get; set; }

    public decimal? Share { get; set; }

    public TimeSpan? Length { get; set; }
}
