// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.DependencyInjection;

namespace NSail.Security.Tests;

public sealed class SessionStandingTests
{
    [Fact]
    public void Standing_is_computed_once_and_again_when_the_organization_moves()
    {
        var here = Guid.NewGuid();
        var there = Guid.NewGuid();
        var session = new Session
        {
            OrganizationId = here,
            Memberships = [new SessionMembership { OrganizationId = here, Role = "a" }, new SessionMembership { OrganizationId = there, Role = "b" }],
        };

        var first = session.Standing();

        Assert.Same(first, session.Standing());
        Assert.Equal("a", Assert.Single(first).Role);

        session.OrganizationId = there;

        Assert.Equal("b", Assert.Single(session.Standing()).Role);

        session.Memberships.Add(new SessionMembership { OrganizationId = there, Role = "c" });

        Assert.Equal(2, session.Standing().Count());
    }

    [Fact]
    public void An_assignment_on_one_scope_does_not_change_another_and_is_undone_when_its_scope_ends()
    {
        var services = new ServiceCollection();
        services.AddScoped<SessionProvider>();
        using var root = services.BuildServiceProvider();

        var outer = new Session { DisplayName = "outer" };
        var inner = new Session { DisplayName = "inner" };

        using var scopeA = root.CreateScope();
        using var scopeB = root.CreateScope();
        var a = scopeA.ServiceProvider.GetRequiredService<SessionProvider>();
        var b = scopeB.ServiceProvider.GetRequiredService<SessionProvider>();

        a.Session = outer;
        b.Session = inner;

        Assert.Same(outer, a.Session);
        Assert.Same(inner, b.Session);

        using (var nested = root.CreateScope())
        {
            Assert.Same(inner, nested.ServiceProvider.GetRequiredService<SessionProvider>().Session);
        }

        scopeB.Dispose();

        using var later = root.CreateScope();
        Assert.Same(outer, later.ServiceProvider.GetRequiredService<SessionProvider>().Session);
        Assert.Same(outer, a.Session);
    }
}
