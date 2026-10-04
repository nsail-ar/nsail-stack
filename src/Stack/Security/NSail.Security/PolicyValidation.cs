// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using NSail.Problems;

namespace NSail.Security;

/// <summary>What the editor learns before saving a policy. Errors block the save; the
/// anonymous flag does not — an audience-less policy is legal and one step from an
/// unauthenticated API, so it is warned about loudly rather than forbidden.</summary>
public sealed class PolicyValidation
{
    public PolicyValidation(IReadOnlyList<Issue> errors, bool isAnonymous)
    {
        ArgumentNullException.ThrowIfNull(errors);

        Errors = errors;
        IsAnonymous = isAnonymous;
    }

    public IReadOnlyList<Issue> Errors { get; }

    public bool IsAnonymous { get; }

    public bool IsValid => Errors.Count == 0;
}
