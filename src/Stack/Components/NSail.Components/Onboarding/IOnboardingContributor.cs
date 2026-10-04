// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components;

/// <summary>Contributes a module's steps to onboarding. Async so a contributor may vary with
/// the session or with what the installation already carries.</summary>
public interface IOnboardingContributor
{
    Task<IReadOnlyList<OnboardingStep>> GetItems();
}
