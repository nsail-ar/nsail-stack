// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components;

/// <summary>Contributed onboarding step: StepType is the component that renders it, and Name
/// is the localization key, the same role it plays on NavMenuItem and DashboardItem. A step
/// is an item in a sequence, not a destination, so it names no route.</summary>
public sealed class OnboardingStep
{
    public required string Name { get; init; }

    public required Type StepType { get; init; }

    /// <summary>Sort key across all contributors; ties keep contribution order.</summary>
    public int Weight { get; init; }

    /// <summary>How the step reports it is already done. A query — whether an organization
    /// exists, whether the admin has a password — so it is awaitable, and it belongs to the
    /// contributor that declared the step, the only party holding the services to answer it.
    /// Absent means the step cannot know, and a step that cannot know is asked rather than
    /// skipped: setup silently left half done costs more than a question asked twice.</summary>
    public Func<Task<bool>>? IsCompleteAsync { get; init; }

    /// <summary>Whether this step, when it is contributed, is the whole sequence. An
    /// installation that is already set up and is asked one thing again walks that one screen
    /// and nothing else — the setup steps behind it have nothing left to collect, and several
    /// of them cannot know that (IsCompleteAsync above), so skipping them one by one is not
    /// something the sequence can work out for itself. The contributor that knows says so.
    /// The lowest-weight claim wins; no claim is the ordinary sequence.</summary>
    public bool ClaimsSequence { get; init; }

    /// <summary>Every contributor's steps in one sequence, ordered by Weight ascending — or
    /// the single step that claimed it.</summary>
    public static async Task<IReadOnlyList<OnboardingStep>> Collect(
        IEnumerable<IOnboardingContributor> contributors)
    {
        var steps = new List<OnboardingStep>();

        foreach (var contributor in contributors)
        {
            steps.AddRange(await contributor.GetItems());
        }

        // OrderBy is stable, so equal weights keep the contribution order above.
        var ordered = steps.OrderBy(step => step.Weight).ToList();

        if (ordered.Find(step => step.ClaimsSequence) is { } claimed)
        {
            return [claimed];
        }

        return ordered;
    }

    /// <summary>Index of the first step that is not already done — where the sequence opens.
    /// Steps answer in order, and a sequence whose every step is complete returns its own
    /// count, one past the last step.</summary>
    public static async Task<int> FirstPending(IReadOnlyList<OnboardingStep> steps)
    {
        for (var index = 0; index < steps.Count; index++)
        {
            if (steps[index].IsCompleteAsync is not { } isComplete || !await isComplete())
            {
                return index;
            }
        }

        return steps.Count;
    }
}
