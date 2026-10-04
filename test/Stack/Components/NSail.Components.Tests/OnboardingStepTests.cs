// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.AspNetCore.Components;
using NSail.Components;

namespace NSail.Components.Tests;

public sealed class FirstTestStep : ComponentBase;

public sealed class SecondTestStep : ComponentBase;

public sealed class ThirdTestStep : ComponentBase;

public sealed class OnboardingStepTests
{
    sealed class TestContributor(params OnboardingStep[] steps) : IOnboardingContributor
    {
        public Task<IReadOnlyList<OnboardingStep>> GetItems()
        {
            return Task.FromResult<IReadOnlyList<OnboardingStep>>(steps);
        }
    }

    static OnboardingStep Step(string name, Type stepType, int weight = 0, bool? complete = null)
    {
        return new OnboardingStep
        {
            Name = name,
            StepType = stepType,
            Weight = weight,
            IsCompleteAsync = complete is null ? null : () => Task.FromResult(complete.Value)
        };
    }

    [Fact]
    public async Task OrdersStepsByWeightAscending()
    {
        var steps = await OnboardingStep.Collect([new TestContributor(
            Step("Late", typeof(ThirdTestStep), 60),
            Step("Early", typeof(FirstTestStep), 5),
            Step("Middle", typeof(SecondTestStep), 30))]);

        Assert.Equal(["Early", "Middle", "Late"], steps.Select(step => step.Name));
    }

    [Fact]
    public async Task OrdersStepsByWeightAcrossContributors()
    {
        var steps = await OnboardingStep.Collect([
            new TestContributor(Step("Late", typeof(ThirdTestStep), 90)),
            new TestContributor(Step("Early", typeof(FirstTestStep), 10))]);

        Assert.Equal(["Early", "Late"], steps.Select(step => step.Name));
    }

    [Fact]
    public async Task KeepsContributionOrderBetweenEqualWeights()
    {
        var steps = await OnboardingStep.Collect([
            new TestContributor(
                Step("First", typeof(FirstTestStep), 10),
                Step("Second", typeof(SecondTestStep), 10)),
            new TestContributor(Step("Third", typeof(ThirdTestStep), 10))]);

        Assert.Equal(["First", "Second", "Third"], steps.Select(step => step.Name));
    }

    [Fact]
    public async Task NoContributorsMeansNoSteps()
    {
        Assert.Empty(await OnboardingStep.Collect([]));
    }

    [Fact]
    public async Task AStepReportingCompleteIsTreatedAsComplete()
    {
        var steps = await OnboardingStep.Collect([new TestContributor(
            Step("Done", typeof(FirstTestStep), 10, complete: true),
            Step("Pending", typeof(SecondTestStep), 20, complete: false))]);

        Assert.Equal(1, await OnboardingStep.FirstPending(steps));
    }

    [Fact]
    public async Task CompletenessIsAskedInWeightOrder()
    {
        var steps = await OnboardingStep.Collect([new TestContributor(
            Step("Pending", typeof(SecondTestStep), 20, complete: false),
            Step("Done", typeof(FirstTestStep), 10, complete: true))]);

        Assert.Equal(1, await OnboardingStep.FirstPending(steps));
    }

    [Fact]
    public async Task AStepThatCannotKnowIsAsked()
    {
        var steps = await OnboardingStep.Collect([new TestContributor(
            Step("Unknown", typeof(FirstTestStep), 10),
            Step("Done", typeof(SecondTestStep), 20, complete: true))]);

        Assert.Equal(0, await OnboardingStep.FirstPending(steps));
    }

    [Fact]
    public async Task EveryStepCompleteAnswersPastTheLast()
    {
        var steps = await OnboardingStep.Collect([new TestContributor(
            Step("First", typeof(FirstTestStep), 10, complete: true),
            Step("Second", typeof(SecondTestStep), 20, complete: true))]);

        Assert.Equal(2, await OnboardingStep.FirstPending(steps));
    }

    // A claim cannot be expressed as completeness: several steps cannot know whether they are
    // done, so a sequence that has one thing left to ask has to be told, not deduced.
    [Fact]
    public async Task AClaimedSequenceIsTheClaimingStepAlone()
    {
        var steps = await OnboardingStep.Collect([
            new TestContributor(Step("Setup", typeof(FirstTestStep), 10)),
            new TestContributor(Claim("Agreement", typeof(SecondTestStep), 80))]);

        Assert.Equal(["Agreement"], steps.Select(step => step.Name));
        Assert.Equal(0, await OnboardingStep.FirstPending(steps));
    }

    [Fact]
    public async Task TheLowestWeightClaimWins()
    {
        var steps = await OnboardingStep.Collect([new TestContributor(
            Claim("Late", typeof(ThirdTestStep), 80),
            Claim("Early", typeof(SecondTestStep), 20))]);

        Assert.Equal(["Early"], steps.Select(step => step.Name));
    }

    // The other half: an unclaimed sequence is every step, which is what every install that is
    // being set up for the first time gets.
    [Fact]
    public async Task NoClaimIsTheOrdinarySequence()
    {
        var steps = await OnboardingStep.Collect([new TestContributor(
            Step("First", typeof(FirstTestStep), 10),
            Step("Second", typeof(SecondTestStep), 20))]);

        Assert.Equal(["First", "Second"], steps.Select(step => step.Name));
    }

    static OnboardingStep Claim(string name, Type stepType, int weight)
    {
        return new OnboardingStep
        {
            Name = name,
            StepType = stepType,
            Weight = weight,
            ClaimsSequence = true,
        };
    }
}
