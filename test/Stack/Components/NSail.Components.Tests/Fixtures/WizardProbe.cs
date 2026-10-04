// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components.Tests.Fixtures;

/// <summary>What the probe steps record and what the test tells them to answer. A step is
/// reached through DynamicComponent, which takes no parameters here, so the conversation
/// with it runs through this shared instance.</summary>
public sealed class WizardProbe
{
    public int Loads { get; set; }

    public int Submits { get; set; }

    public bool Vetoes { get; set; }

    public bool CanAdvance { get; set; } = true;

    /// <summary>What the step's own hook waits on, so a test can look at the chrome while a
    /// Next is still in flight — a step's hook is its form's Submit(), which is a round trip.</summary>
    public TaskCompletionSource<bool>? Held { get; set; }

    public event Action? Changed;

    public void Report(bool canAdvance)
    {
        CanAdvance = canAdvance;
        Changed?.Invoke();
    }
}
