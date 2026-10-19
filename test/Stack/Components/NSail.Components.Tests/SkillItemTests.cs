// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Components.Tests.Fixtures;
using NSail.Messaging.Runtime;
using NSail.Metadata;

namespace NSail.Components.Tests;

/// <summary>The Assistant's composition seam: a kit's playbooks reach the model because its
/// module registered a contributor, the same way a menu entry or a guide chapter does — and a
/// body costs nothing until something asks for that one skill by name.</summary>
public sealed class SkillItemTests
{
    sealed class SomeMessage;

    static SkillItem Skill(string name, int weight = 0, string? description = null, params string[] tools)
    {
        return new SkillItem
        {
            Name = name,
            Description = description ?? $"Use {name} when the person asks for it.",
            Tools = tools,
            Weight = weight
        };
    }

    static FixedSkillContributor Contributor(params SkillItem[] items)
    {
        return new FixedSkillContributor(
            items.ToDictionary(item => item.Name, item => $"Steps for {item.Name}."),
            items);
    }

    [Fact]
    public async Task OrdersSkillsByWeightAscending()
    {
        var skills = await SkillItem.Collect([Contributor(
            Skill("late", 30),
            Skill("early", 10),
            Skill("middle", 20))]);

        Assert.Equal(["early", "middle", "late"], skills.Select(skill => skill.Name));
    }

    [Fact]
    public async Task OrdersSkillsByWeightAcrossContributors()
    {
        var skills = await SkillItem.Collect([
            Contributor(Skill("late", 30)),
            Contributor(Skill("early", 10))]);

        Assert.Equal(["early", "late"], skills.Select(skill => skill.Name));
    }

    [Fact]
    public async Task KeepsContributionOrderBetweenEqualWeights()
    {
        var skills = await SkillItem.Collect([
            Contributor(Skill("first", 10), Skill("second", 10)),
            Contributor(Skill("third", 10))]);

        Assert.Equal(["first", "second", "third"], skills.Select(skill => skill.Name));
    }

    // The merge rule the AC calls out by name: a later contributor naming a Name already seen
    // replaces the earlier skill WHOLE, so its Tools never carry over into the override.
    [Fact]
    public async Task ALaterContributorReplacesAnEarlierSkillOfTheSameNameWhole()
    {
        var skills = await SkillItem.Collect([
            Contributor(Skill("record-expense", weight: 10, description: "Kit's own.", tools: "Accounting.CreateJournalEntry")),
            Contributor(Skill("record-expense", weight: 90, description: "App's own.", tools: "Accounting.CreateBill"))]);

        var skill = Assert.Single(skills);

        Assert.Equal("App's own.", skill.Description);
        Assert.Equal(90, skill.Weight);
        Assert.Equal(["Accounting.CreateBill"], skill.Tools);
    }

    // An override keeps its first-appearance SLOT even though its content is the later
    // contributor's. The override here lands on the SAME weight as the skill beside it, so the
    // two are tied: OrderBy's stable sort alone would read the input order it was just given —
    // ["sell", "record-expense"] — and only the slot kept from first appearance puts
    // "record-expense" back ahead of "sell".
    [Fact]
    public async Task AnOverrideKeepsItsFirstAppearanceSlot()
    {
        var skills = await SkillItem.Collect([
            Contributor(Skill("record-expense", weight: 10), Skill("sell", weight: 10)),
            Contributor(Skill("record-expense", weight: 10))]);

        Assert.Equal(["record-expense", "sell"], skills.Select(skill => skill.Name));
    }

    [Fact]
    public async Task AModuleThatContributesNothingIsAbsent()
    {
        Assert.Empty(await SkillItem.Collect([]));
        Assert.Empty(await SkillItem.Collect([Contributor()]));
    }

    // Lazy by construction: Collect lists every skill without ever calling GetBody, and a
    // reader who never asks for a body never costs its owning contributor a read.
    [Fact]
    public async Task CollectingSkillsReadsNoBody()
    {
        var contributor = Contributor(Skill("record-expense"));

        await SkillItem.Collect([contributor]);

        Assert.Equal(0, contributor.BodyReads);
    }

    [Fact]
    public async Task ASkillsBodyIsReadFromItsOwningContributorOnlyWhenAsked()
    {
        var contributor = Contributor(Skill("record-expense"));
        var skill = Assert.Single(await SkillItem.Collect([contributor]));

        var body = await skill.GetBody();

        Assert.Equal("Steps for record-expense.", body);
        Assert.Equal(1, contributor.BodyReads);
    }

    // After an override, the body a reader gets is the WINNING contributor's — the slot
    // carries the later contributor's identity along with its other fields.
    [Fact]
    public async Task AnOverriddenSkillsBodyComesFromTheOverridingContributor()
    {
        var kit = Contributor(Skill("record-expense"));
        var app = Contributor(Skill("record-expense"));
        var skill = Assert.Single(await SkillItem.Collect([kit, app]));

        await skill.GetBody();

        Assert.Equal(0, kit.BodyReads);
        Assert.Equal(1, app.BodyReads);
    }

    static MessageRegistry Registry()
    {
        return new MessageRegistry(new MetadataProvider(), [typeof(SomeMessage)]);
    }

    [Fact]
    public async Task VerifyPassesACleanSkill()
    {
        var registry = Registry();
        var key = registry.KeyFor(typeof(SomeMessage));
        var skills = await SkillItem.Collect([Contributor(Skill("record-expense", tools: key))]);

        var problems = await SkillItem.Verify(skills, registry);

        Assert.Empty(problems);
    }

    [Fact]
    public async Task VerifyNamesTheSkillAndTheKeyWhenAToolIsUnknown()
    {
        var skills = await SkillItem.Collect([Contributor(Skill("record-expense", tools: "Nothing.Here"))]);

        var problems = await SkillItem.Verify(skills, Registry());

        Assert.Contains(problems, problem => problem.Contains("record-expense") && problem.Contains("Nothing.Here"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task VerifyFlagsAnEmptyDescription(string description)
    {
        var skills = await SkillItem.Collect([Contributor(Skill("record-expense", description: description))]);

        var problems = await SkillItem.Verify(skills, Registry());

        Assert.Contains(problems, problem => problem.Contains("record-expense") && problem.Contains("Description"));
    }

    [Fact]
    public async Task VerifyFlagsADescriptionOverItsBound()
    {
        var skills = await SkillItem.Collect(
            [Contributor(Skill("record-expense", description: new string('x', SkillItem.MaxDescriptionLength + 1)))]);

        var problems = await SkillItem.Verify(skills, Registry());

        Assert.Contains(problems, problem => problem.Contains("record-expense") && problem.Contains("Description"));
    }

    [Fact]
    public async Task VerifyFlagsABodyOverItsBound()
    {
        var contributor = new FixedSkillContributor(
            new Dictionary<string, string> { ["record-expense"] = new string('x', SkillItem.MaxBodyLength + 1) },
            Skill("record-expense"));
        var skills = await SkillItem.Collect([contributor]);

        var problems = await SkillItem.Verify(skills, Registry());

        Assert.Contains(problems, problem => problem.Contains("record-expense") && problem.Contains("Body"));
    }
}
