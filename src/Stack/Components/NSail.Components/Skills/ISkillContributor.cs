// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components;

/// <summary>Contributes a module's playbooks to the Assistant, the way a module contributes
/// its menu entries or its guide chapters — without depending on the Assistant kit at all.
/// A skill is written for the MODEL, once, in English, and is never shown to a person: it is
/// not a guide chapter, which is prose a reader opens, but an instruction an agent follows
/// ("to record an expense, use this template and these accounts"). GetSkills is cheap — a
/// host that only lists what is available reads no body — and GetBody fetches one skill's
/// prose by name, so a host never holds a body it was not asked for.</summary>
public interface ISkillContributor
{
    Task<IReadOnlyList<SkillItem>> GetSkills();

    Task<string> GetBody(string name);
}
