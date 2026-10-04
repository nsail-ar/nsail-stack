// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using NSail.Dates;
using NSail.Security.Annotations;

namespace NSail.Security.Tests;

// Namespace-derived keys: "Security.Tests.OrderTestThing", "Security.Tests.LookupTestThings",
// "Security.Tests.LookupTestPeople".
public sealed class OrderTestThing
{
    public Guid? PartyId { get; set; }
}

public sealed class LookupTestThings
{
    public Guid? OrganizationId { get; set; }
}

// The second hop's target: what a chain would reach if the evaluator let one form.
public sealed class LookupTestPeople
{
}

internal sealed class OrderTestThingPolicyHandler : PolicyHandler
{
    public OrderTestThingPolicyHandler(Policy policy)
        : base(policy)
    {
    }

    public override async Task<bool> Authorize(object message, Session session, RelationProvider relations, CancellationToken cancellationToken)
    {
        if (message is not OrderTestThing m)
            return false;

        return await Satisfies("PartyId", m.PartyId, RestrictAs.Party, session, relations, cancellationToken);
    }
}

internal sealed class LookupTestThingsPolicyHandler : PolicyHandler
{
    public LookupTestThingsPolicyHandler(Policy policy)
        : base(policy)
    {
    }

    public override async Task<bool> Authorize(object message, Session session, RelationProvider relations, CancellationToken cancellationToken)
    {
        if (message is not LookupTestThings m)
            return false;

        return await Satisfies("OrganizationId", m.OrganizationId, RestrictAs.Organization, session, relations, cancellationToken);
    }
}

internal sealed class LookupTestPeoplePolicyHandler : PolicyHandler
{
    public LookupTestPeoplePolicyHandler(Policy policy)
        : base(policy)
    {
    }

    public override Task<bool> Authorize(object message, Session session, RelationProvider relations, CancellationToken cancellationToken)
    {
        return Task.FromResult(message is LookupTestPeople);
    }
}

// permissions.md, [Requires]: a message is allowed if a policy allows it, OR if some allowed
// message declares it. Nothing here names a lookup in a policy — the grants are all over the
// primary, and what the lookup sends prove is the implication doing the granting.
public sealed class DependencyPolicyTests
{
    const string PrimaryKey = "Security.Tests.OrderTestThing";
    const string LookupKey = "Security.Tests.LookupTestThings";

    static readonly Guid Me = Guid.NewGuid();
    static readonly Guid Other = Guid.NewGuid();
    static readonly Guid Hospital = Guid.NewGuid();
    static readonly Guid Elsewhere = Guid.NewGuid();

    static Session Seller(string role = "vendedor")
    {
        return new()
        {
            IsAuthenticated = true,
            PartyId = Me,
            OrganizationId = Hospital,
            Roles = [role],
        };
    }

    static SecurityManager Manager(params Policy[] policies)
    {
        return Manager(chained: false, policies);
    }

    static SecurityManager Manager(bool chained, params Policy[] policies)
    {
        var factories = new[]
        {
            new PolicyHandlerFactory(typeof(OrderTestThing), p => new OrderTestThingPolicyHandler(p))
            {
                Fields = new Dictionary<string, RestrictAs> { ["PartyId"] = RestrictAs.Party },
                Requires = [typeof(LookupTestThings)],
            },
            new PolicyHandlerFactory(typeof(LookupTestThings), p => new LookupTestThingsPolicyHandler(p))
            {
                Fields = new Dictionary<string, RestrictAs> { ["OrganizationId"] = RestrictAs.Organization },
                Requires = chained ? [typeof(LookupTestPeople)] : [],
            },
            new PolicyHandlerFactory(typeof(LookupTestPeople), p => new LookupTestPeoplePolicyHandler(p)),
        };

        return new SecurityManager(Registry.For(factories), new FakeRelations(), factories, policies);
    }

    static Policy PrimaryFor(string role)
    {
        return new Policy { Name = "orders", Messages = [PrimaryKey], Audience = new() { HasRole = role } };
    }

    [Fact]
    public async Task The_holder_of_the_primary_may_send_what_it_declares_needing()
    {
        var manager = Manager(PrimaryFor("vendedor"));

        Assert.True((await manager.Authorize(new LookupTestThings(), Seller())).Allowed);
    }

    [Fact]
    public async Task Nobody_else_may()
    {
        var manager = Manager(PrimaryFor("vendedor"));

        Assert.False((await manager.Authorize(new LookupTestThings(), Seller("recepcion"))).Allowed);
    }

    [Fact]
    public async Task Without_the_declaration_the_lookup_is_denied_to_the_same_holder()
    {
        var factories = new[]
        {
            new PolicyHandlerFactory(typeof(OrderTestThing), p => new OrderTestThingPolicyHandler(p)),
            new PolicyHandlerFactory(typeof(LookupTestThings), p => new LookupTestThingsPolicyHandler(p)),
        };

        var manager = new SecurityManager(Registry.For(factories), new FakeRelations(), factories, [PrimaryFor("vendedor")]);

        Assert.False((await manager.Authorize(new LookupTestThings(), Seller())).Allowed);
    }

    [Fact]
    public async Task The_match_says_it_was_the_implication_and_which_message_carried_it()
    {
        var manager = Manager(PrimaryFor("vendedor"));

        var result = await manager.Authorize(new LookupTestThings(), Seller());

        var match = Assert.Single(result.Matches);

        Assert.Equal(PolicyOrigin.Dependency, match.Origin);
        Assert.Equal(PrimaryKey, Assert.IsType<DependencyPolicyHandler>(match).PrimaryKey);
        Assert.Contains(PrimaryKey, match.Policy.Name, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_primary_itself_is_still_allowed_by_the_policy_that_granted_it()
    {
        var manager = Manager(PrimaryFor("vendedor"));

        var result = await manager.Authorize(new OrderTestThing { PartyId = Me }, Seller());

        Assert.Equal(PolicyOrigin.BuiltIn, Assert.Single(result.Matches).Origin);
    }

    // Guardrail 1. The chain LookupTestThings → LookupTestPeople is declared and live; what is
    // refused is reaching it through an implication rather than a policy.
    [Fact]
    public async Task One_hop_no_transitivity()
    {
        var manager = Manager(chained: true, PrimaryFor("vendedor"));

        Assert.True((await manager.Authorize(new LookupTestThings(), Seller())).Allowed);
        Assert.False((await manager.Authorize(new LookupTestPeople(), Seller())).Allowed);

        var direct = Manager(chained: true, new Policy { Name = "lookups", Messages = [LookupKey], Audience = new() { HasRole = "vendedor" } });

        Assert.True((await direct.Authorize(new LookupTestPeople(), Seller())).Allowed);
    }

    // Guardrails 2 and 3 in one send: the primary's grant is narrowed to the caller's own
    // party and the lookup's own row to the caller's own organization, and the implication
    // still carries a send that violates both — flat, and ORing above the narrower row.
    [Fact]
    public async Task The_implication_is_flat_and_the_narrowed_row_cannot_restrict_below_it()
    {
        var manager = Manager(
            new Policy
            {
                Name = "orders",
                Messages = [PrimaryKey],
                Audience = new() { HasRole = "vendedor" },
                Fields = new Dictionary<string, PolicyConstraint> { ["PartyId"] = PolicyConstraint.Me },
            },
            new Policy
            {
                Name = "lookups, narrowed",
                Messages = [LookupKey],
                Audience = new() { HasRole = "vendedor" },
                Fields = new Dictionary<string, PolicyConstraint> { ["OrganizationId"] = PolicyConstraint.Current },
            });

        Assert.False((await manager.Authorize(new OrderTestThing { PartyId = Other }, Seller())).Allowed);
        Assert.True((await manager.Authorize(new LookupTestThings { OrganizationId = Elsewhere }, Seller())).Allowed);
    }

    // Guardrail 4: the verdict says the fields went unread, which is what stops the row filter
    // following the organization this send names (nsail#1600). The same send that guardrail 2
    // lets through carries the narrowed policy that REFUSED it — so "some policy applies to the
    // key" is not the question, "what actually allowed this send" is.
    [Fact]
    public async Task A_send_the_implication_alone_allowed_reports_its_fields_unread()
    {
        var manager = Manager(
            PrimaryFor("vendedor"),
            new Policy
            {
                Name = "lookups, narrowed",
                Messages = [LookupKey],
                Audience = new() { HasRole = "vendedor" },
                Fields = new Dictionary<string, PolicyConstraint> { ["OrganizationId"] = PolicyConstraint.Current },
            });

        Assert.False((await manager.Authorize(new LookupTestThings { OrganizationId = Elsewhere }, Seller())).FieldsVetted);
        Assert.True((await manager.Authorize(new LookupTestThings { OrganizationId = Hospital }, Seller())).FieldsVetted);
    }

    // And the primary's own send, which a policy allowed on its values, is vetted — the
    // implication beside it never takes that away.
    [Fact]
    public async Task A_send_a_policy_allowed_reports_its_fields_read()
    {
        var manager = Manager(PrimaryFor("vendedor"));

        Assert.True((await manager.Authorize(new OrderTestThing { PartyId = Me }, Seller())).FieldsVetted);
    }

    // A session the gate waves through consulted no policy at all, so there is no unread field
    // to report: the org filter takes what a job's message names.
    [Fact]
    public async Task The_system_session_reports_its_fields_read()
    {
        var manager = Manager(PrimaryFor("vendedor"));

        var result = await manager.Authorize(new LookupTestThings { OrganizationId = Elsewhere }, Session.System());

        Assert.True(result.Allowed);
        Assert.True(result.FieldsVetted);
    }

    [Fact]
    public async Task An_expired_primary_implies_nothing()
    {
        var manager = Manager(new Policy
        {
            Name = "orders, last year",
            Messages = [PrimaryKey],
            Audience = new() { HasRole = "vendedor" },
            ValidTo = BusinessDate.Today.AddDays(-1),
        });

        Assert.False((await manager.Authorize(new LookupTestThings(), Seller())).Allowed);
    }

    [Fact]
    public void Visibility_follows_the_implication()
    {
        var manager = Manager(PrimaryFor("vendedor"));

        Assert.True(manager.PreAuthorize(typeof(LookupTestThings), Seller()));
        Assert.False(manager.PreAuthorize(typeof(LookupTestThings), Seller("recepcion")));
    }

    // The closure the client is handed: it never learns implications exist, it just finds the
    // lookup's key among the policies it may send.
    [Fact]
    public void The_effective_policies_carry_the_closure()
    {
        var manager = Manager(PrimaryFor("vendedor"));

        Assert.Contains(manager.GetEffectivePolicies(Seller()), policy => policy.Messages.Contains(LookupKey));
        Assert.DoesNotContain(manager.GetEffectivePolicies(Seller("recepcion")), policy => policy.Messages.Contains(LookupKey));
    }
}
