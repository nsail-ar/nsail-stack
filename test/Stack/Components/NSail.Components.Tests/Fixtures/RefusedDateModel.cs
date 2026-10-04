// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components.Tests.Fixtures;

/// <summary>The date shapes the real pages actually bind — both NON-NULLABLE, which is where a
/// refusal written through does its damage: a <c>DateOnly</c> takes 0001-01-01 (the OT's promise
/// date, the sale's, the prescription's) and a <c>DateTime</c> takes today at the hour already
/// chosen (the turno's start), and both look like ordinary dates to everything downstream.</summary>
public sealed class RefusedDateModel
{
    public DateOnly Delivery { get; set; }

    public DateTime Start { get; set; }
}
