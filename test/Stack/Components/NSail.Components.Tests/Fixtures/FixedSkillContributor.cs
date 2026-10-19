// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components.Tests.Fixtures;

/// <summary>A module's skills, given rather than composed: the items a test wants, and bodies
/// keyed by Name so a test can tell whether GetBody was ever asked for one.</summary>
public sealed class FixedSkillContributor(IReadOnlyDictionary<string, string> bodies, params SkillItem[] items)
    : ISkillContributor
{
    public int BodyReads { get; private set; }

    public Task<IReadOnlyList<SkillItem>> GetSkills()
    {
        return Task.FromResult<IReadOnlyList<SkillItem>>(items);
    }

    public Task<string> GetBody(string name)
    {
        BodyReads++;

        return Task.FromResult(bodies[name]);
    }
}
