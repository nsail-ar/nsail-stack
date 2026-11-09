// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Data.Tests;

public sealed class OrgScopeProviderTests
{
    [Fact]
    public async Task overlapping_read_everywhere_restores_the_original_scope()
    {
        var provider = new OrgScopeProvider();
        var standing = OrgScope.Nowhere;
        var first = new TaskCompletionSource();
        var second = new TaskCompletionSource();

        provider.Enter(standing);

        var a = provider.ReadEverywhere(async () =>
        {
            await first.Task;

            return 1;
        });
        var b = provider.ReadEverywhere(async () =>
        {
            await second.Task;

            return 2;
        });

        Assert.Same(OrgScope.Everywhere, provider.Current);

        first.SetResult();
        await a;
        Assert.Same(OrgScope.Everywhere, provider.Current);

        second.SetResult();
        await b;
        Assert.Same(standing, provider.Current);
    }

    [Fact]
    public async Task read_everywhere_finishing_out_of_order_restores_the_original_scope()
    {
        var provider = new OrgScopeProvider();
        var standing = OrgScope.Nowhere;
        var first = new TaskCompletionSource();
        var second = new TaskCompletionSource();

        provider.Enter(standing);

        var a = provider.ReadEverywhere(async () =>
        {
            await first.Task;

            return 1;
        });
        var b = provider.ReadEverywhere(async () =>
        {
            await second.Task;

            return 2;
        });

        second.SetResult();
        await b;
        first.SetResult();
        await a;

        Assert.Same(standing, provider.Current);
    }
}
