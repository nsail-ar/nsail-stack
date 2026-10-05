// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Messaging.Runtime.Validation;

namespace NSail.Components.Tests.Fixtures;

public sealed class FormProblemModel
{
    public string? Name { get; set; }

    public string? Alias { get; set; }

    /// <summary>A real property of the model that no field in the host renders — the shape
    /// that used to swallow a refusal whole (6062c3fb).</summary>
    public string? Nickname { get; set; }

    /// <summary>The same shape with a rule of its OWN: a member no field renders whose refusal
    /// is decided on the client, by a coded attribute, with no send to carry one back. Zero
    /// passes, so a host that is not about this rule never meets it.</summary>
    [NotNegative]
    public int Score { get; set; }
}
