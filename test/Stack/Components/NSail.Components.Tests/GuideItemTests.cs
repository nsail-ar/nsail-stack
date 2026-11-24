// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSail.Components;
using NSail.Components.Tests.Fixtures;

namespace NSail.Components.Tests;

/// <summary>The guide's composition seam: a chapter reaches an app because its module
/// registered a contributor, and for no other reason — and it names a file beside that module
/// rather than carrying its own prose.</summary>
public sealed class GuideItemTests
{
    sealed class TestContributor(params GuideItem[] items) : IGuideContributor
    {
        public Task<IReadOnlyList<GuideItem>> GetItems()
        {
            return Task.FromResult<IReadOnlyList<GuideItem>>(items);
        }
    }

    static GuideItem Chapter(string name, int weight = 0)
    {
        return new GuideItem { Name = name, File = name.ToLowerInvariant(), Weight = weight };
    }

    [Fact]
    public async Task OrdersChaptersByWeightAscending()
    {
        var chapters = await GuideItem.Collect([new TestContributor(
            Chapter("Late", 30),
            Chapter("Early", 10),
            Chapter("Middle", 20))]);

        Assert.Equal(["Early", "Middle", "Late"], chapters.Select(chapter => chapter.Name));
    }

    [Fact]
    public async Task OrdersChaptersByWeightAcrossContributors()
    {
        var chapters = await GuideItem.Collect([
            new TestContributor(Chapter("Late", 30)),
            new TestContributor(Chapter("Early", 10))]);

        Assert.Equal(["Early", "Late"], chapters.Select(chapter => chapter.Name));
    }

    [Fact]
    public async Task KeepsContributionOrderBetweenEqualWeights()
    {
        var chapters = await GuideItem.Collect([
            new TestContributor(Chapter("First", 10), Chapter("Second", 10)),
            new TestContributor(Chapter("Third", 10))]);

        Assert.Equal(["First", "Second", "Third"], chapters.Select(chapter => chapter.Name));
    }

    // The composition rule itself: nothing filters a chapter out afterwards, so a module
    // that contributed nothing is simply absent from the guide it never joined.
    [Fact]
    public async Task AModuleThatContributesNothingIsAbsent()
    {
        Assert.Empty(await GuideItem.Collect([]));
        Assert.Empty(await GuideItem.Collect([new TestContributor()]));
    }

    // A contributor and the wwwroot that carries its chapters are one project by construction,
    // so nothing writes the assembly name — the file is served from the contributor's own.
    [Fact]
    public async Task AChapterIsServedFromTheContributingModuleStaticAssets()
    {
        var chapter = Assert.Single(await GuideItem.Collect([new TestContributor(Chapter("Sales"))]));

        Assert.Equal("NSail.Components.Tests", chapter.Assembly);
        Assert.Equal("_content/NSail.Components.Tests/guide/es/sales.md", chapter.Path("es"));
        Assert.Equal("_content/NSail.Components.Tests/guide/es/", chapter.Folder("es"));
        Assert.Equal("_content/NSail.Components.Tests/guide/en/sales.md", chapter.Path("en"));
    }

    [Fact]
    public async Task AnAddressThatNamesNoChapterOpensTheFirst()
    {
        var chapters = await GuideItem.Collect([new TestContributor(
            Chapter("Sales", 10),
            Chapter("Accounting", 30))]);

        Assert.Equal("Sales", GuideItem.Open(chapters, null)?.Name);
        Assert.Equal("Sales", GuideItem.Open(chapters, "gone")?.Name);
        Assert.Equal("Accounting", GuideItem.Open(chapters, "accounting")?.Name);
        Assert.Equal("Accounting", GuideItem.Open(chapters, "ACCOUNTING")?.Name);
        Assert.Null(GuideItem.Open([], "sales"));
    }

    sealed class StaffGrant;

    static PageGate Gate(params Type[] granted)
    {
        var services = new ServiceCollection();

        services.AddAuthorizationCore();
        services.AddSingleton<IAuthorizationPolicyProvider, MessagePolicyProvider>();
        services.AddSingleton<IAuthorizationHandler>(new GrantedMessages(granted));
        services.AddSingleton<ILoggerFactory, NullLoggerFactory>();
        services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));

        var provider = services.BuildServiceProvider();

        return new PageGate(
            provider.GetRequiredService<IAuthorizationService>(),
            provider.GetRequiredService<IAuthorizationPolicyProvider>());
    }

    static IEnumerable<IGuideContributor> Contributors()
    {
        return [new TestContributor(
            new GuideItem { Name = "Staff", File = "staff", Permission = typeof(StaffGrant) },
            Chapter("Open"))];
    }

    [Fact]
    public async Task DropsChaptersWhoseGrantTheSessionLacks()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity("test"));

        var chapters = await GuideItem.Collect(Contributors(), Gate(), user);

        Assert.Equal(["Open"], chapters.Select(chapter => chapter.Name));
    }

    [Fact]
    public async Task KeepsChaptersWhoseGrantTheSessionHolds()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity("test"));

        var chapters = await GuideItem.Collect(Contributors(), Gate(typeof(StaffGrant)), user);

        Assert.Equal(["Staff", "Open"], chapters.Select(chapter => chapter.Name));
    }
}
