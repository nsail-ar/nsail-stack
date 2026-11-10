// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Harness.Hosting;

namespace NSail.Harness.Tests;

// A status text links the stories and PRs it names, and only those: an entity the page escapes is no number.
public class StatusLinksTests
{
    readonly HarnessOptions _options = new()
    {
        Repositories = [new RepositoryOptions { Name = "acme/app" }, new RepositoryOptions { Name = "acme/nsail-stack", Label = "area:stack" }],
    };

    [Fact]
    public void AnApostropheAndAnAccentLinkToNoIssue()
    {
        var html = StatusPage.Linked(_options, "can't build é — it's \"red\" & <odd>");

        Assert.DoesNotContain("<a ", html);
        Assert.DoesNotContain("issues/39", html);
        Assert.DoesNotContain("issues/233", html);
        Assert.Contains("&#39;", html);
    }

    [Fact]
    public void AStoryAndAnotherRepositorysPrStillLink()
    {
        var html = StatusPage.Linked(_options, "dev #3290 (it's blocked by nsail-stack PR #130)");

        Assert.Contains("<a href=\"https://github.com/acme/app/issues/3290\">#3290</a>", html);
        Assert.Contains("nsail-stack PR <a href=\"https://github.com/acme/nsail-stack/issues/130\">#130</a>", html);
        Assert.DoesNotContain("issues/39\"", html);
    }
}
