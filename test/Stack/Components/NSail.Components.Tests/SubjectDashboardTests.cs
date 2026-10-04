// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.DependencyInjection;
using NSail.Components;

namespace NSail.Components.Tests;

public sealed class TestSubject
{
    public required string Name { get; init; }
}

public sealed class OtherTestSubject;

public sealed class SubjectDashboardTests
{
    sealed class SubjectContributor : IDashboardContributor<TestSubject>
    {
        public Task<IReadOnlyList<DashboardItem>> GetItems(TestSubject subject)
        {
            return Task.FromResult<IReadOnlyList<DashboardItem>>(
            [
                new DashboardItem { Name = subject.Name, CardType = typeof(OpenTestCard) },
            ]);
        }
    }

    sealed class BothContributor : IDashboardContributor, IDashboardContributor<TestSubject>
    {
        public Task<IReadOnlyList<DashboardItem>> GetItems()
        {
            return Task.FromResult<IReadOnlyList<DashboardItem>>(
            [
                new DashboardItem { Name = "App", CardType = typeof(OpenTestCard) },
            ]);
        }

        public Task<IReadOnlyList<DashboardItem>> GetItems(TestSubject subject)
        {
            return Task.FromResult<IReadOnlyList<DashboardItem>>(
            [
                new DashboardItem { Name = "Subject", CardType = typeof(SecondOpenTestCard) },
            ]);
        }
    }

    sealed class NotAContributor;

    static ServiceProvider Register<TContributor>()
        where TContributor : class
    {
        var services = new ServiceCollection();

        services.AddDashboard<TContributor>();

        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task ASubjectContributorIsResolvedForItsOwnSubject()
    {
        var provider = Register<SubjectContributor>();

        var contributors = provider.GetServices<IDashboardContributor<TestSubject>>().ToList();
        var items = await Assert.Single(contributors).GetItems(new TestSubject { Name = "Ana" });

        Assert.Equal("Ana", Assert.Single(items).Name);
    }

    // The subject types the dashboard: a host carrying another subject must not collect a
    // contributor's cards just because both are dashboards.
    [Fact]
    public void ASubjectContributorIsInvisibleToAnotherSubject()
    {
        var provider = Register<SubjectContributor>();

        Assert.Empty(provider.GetServices<IDashboardContributor<OtherTestSubject>>());
        Assert.Empty(provider.GetServices<IDashboardContributor>());
    }

    [Fact]
    public void OneClassMayContributeToTheAppDashboardAndToASubject()
    {
        var provider = Register<BothContributor>();

        Assert.Single(provider.GetServices<IDashboardContributor>());
        Assert.Single(provider.GetServices<IDashboardContributor<TestSubject>>());
    }

    [Fact]
    public void RegisteringSomethingThatContributesNothingThrows()
    {
        Assert.Throws<ArgumentException>(() => new ServiceCollection().AddDashboard<NotAContributor>());
    }
}
