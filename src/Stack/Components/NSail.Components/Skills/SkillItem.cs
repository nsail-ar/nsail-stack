// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Messaging.Runtime;

namespace NSail.Components;

/// <summary>Contributed skill: Name is the kebab-case id a model's tool call names, Description
/// is the one line a host lists it by (when to use it, never over MaxDescriptionLength), Tools
/// names the message keys the skill's steps send (a kit's own vocabulary, not a role), and
/// Weight is the sort key across all contributors (ties keep contribution order, the same bands
/// NavMenuItem's Weight uses). The body costs no I/O until GetBody is called: Collect stamps
/// which contributor answers it, so a host that only listed skills never read one.</summary>
public sealed class SkillItem
{
    public const int MaxDescriptionLength = 160;

    /// <summary>The bound a skill's steps must fit so a model's context never pays for one
    /// skill what it should pay for a handful.</summary>
    public const int MaxBodyLength = 4000;

    public required string Name { get; init; }

    public required string Description { get; init; }

    public required IReadOnlyList<string> Tools { get; init; }

    public int Weight { get; init; }

    ISkillContributor? _contributor;

    public Task<string> GetBody()
    {
        if (_contributor is null)
        {
            throw new InvalidOperationException(
                $"Skill '{Name}' did not come from SkillItem.Collect, so no contributor can answer its body.");
        }

        return _contributor.GetBody(Name);
    }

    /// <summary>Every contributor's skills in one list, ordered by Weight ascending. A later
    /// contributor naming a Name already seen REPLACES the earlier one whole — Tools, Description
    /// and Weight all come from the winner, none of it merged field by field the way the menu's
    /// items are, so an app overriding a kit's skill does not inherit a Tool list it never named.
    /// The replaced skill keeps its first-appearance slot — the override's own Weight still
    /// moves it, the slot only breaks a tie among skills that land on the same Weight.</summary>
    public static async Task<IReadOnlyList<SkillItem>> Collect(IEnumerable<ISkillContributor> contributors)
    {
        ArgumentNullException.ThrowIfNull(contributors);

        var order = new List<string>();
        var byName = new Dictionary<string, SkillItem>(StringComparer.Ordinal);

        foreach (var contributor in contributors)
        {
            foreach (var item in await contributor.GetSkills())
            {
                item._contributor = contributor;

                if (!byName.ContainsKey(item.Name))
                {
                    order.Add(item.Name);
                }

                byName[item.Name] = item;
            }
        }

        // OrderBy is stable, so equal weights keep the first-appearance order above.
        return order.Select(name => byName[name]).OrderBy(item => item.Weight).ToList();
    }

    /// <summary>What a build's own guard test runs over a host's composed contributors: every
    /// skill whose Tools names a key the host's MessageRegistry does not resolve (a message
    /// renamed or removed out from under it), and every skill whose Description or Body is
    /// empty or past its bound. Reading every Body is the cost of this call — a lister never
    /// pays it, a build check pays it once.</summary>
    public static async Task<IReadOnlyList<string>> Verify(
        IReadOnlyList<SkillItem> skills,
        MessageRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(skills);
        ArgumentNullException.ThrowIfNull(registry);

        var problems = new List<string>();

        foreach (var skill in skills)
        {
            if (string.IsNullOrWhiteSpace(skill.Description) || skill.Description.Length > MaxDescriptionLength)
            {
                problems.Add(
                    $"Skill '{skill.Name}': Description is empty or over {MaxDescriptionLength} characters.");
            }

            foreach (var tool in skill.Tools)
            {
                if (registry.Resolve(tool) is null)
                {
                    problems.Add(
                        $"Skill '{skill.Name}': Tools names '{tool}', which the host's MessageRegistry does not know.");
                }
            }

            var body = await skill.GetBody();

            if (string.IsNullOrWhiteSpace(body) || body.Length > MaxBodyLength)
            {
                problems.Add($"Skill '{skill.Name}': Body is empty or over {MaxBodyLength} characters.");
            }
        }

        return problems;
    }
}
