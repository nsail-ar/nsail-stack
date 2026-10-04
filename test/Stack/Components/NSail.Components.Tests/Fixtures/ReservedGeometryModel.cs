// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components.Tests.Fixtures;

public sealed class ReservedGeometryModel
{
    public string? Name { get; set; }

    public Guid? Choice { get; set; }

    public DateOnly? Date { get; set; }

    /// <summary>A real property no field renders: the refusal naming it has to fall to the
    /// form's foot line, which is the state the reserved foot geometry is measured against.</summary>
    public string? Nickname { get; set; }
}
