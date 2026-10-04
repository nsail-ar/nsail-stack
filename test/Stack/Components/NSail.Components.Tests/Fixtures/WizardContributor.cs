// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components.Tests.Fixtures;

public sealed class WizardContributor(params OnboardingStep[] steps) : IOnboardingContributor
{
    public Task<IReadOnlyList<OnboardingStep>> GetItems()
    {
        return Task.FromResult<IReadOnlyList<OnboardingStep>>(steps);
    }
}
