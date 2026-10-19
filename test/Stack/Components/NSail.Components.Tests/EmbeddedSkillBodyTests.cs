// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components.Tests;

/// <summary>The one line a kit's own GetBody stays — reading its own assembly's embedded
/// markdown, the server process's file and never a wwwroot page fetched over HTTP.</summary>
public sealed class EmbeddedSkillBodyTests
{
    [Fact]
    public async Task ReadsTheEmbeddedFileNamedAfterTheSkill()
    {
        var body = await EmbeddedSkillBody.Read(typeof(EmbeddedSkillBodyTests).Assembly, "sample-skill");

        Assert.Equal("Steps for the sample skill.\n", body);
    }

    [Fact]
    public async Task ThrowsWhenNoFileAnswersTheName()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => EmbeddedSkillBody.Read(typeof(EmbeddedSkillBodyTests).Assembly, "no-such-skill"));
    }
}
