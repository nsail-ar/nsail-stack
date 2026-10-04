// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Security.Annotations;
using Xunit;

namespace NSail.Security.Tests;

// The shapes an organization field arrives in: the scalar one almost every list carries, the
// optional one a re-parenting message carries beside it, and the collection the evaluator
// already quantifies over. A party and a reference field ride along so the tests can prove the
// AXIS is what is read, and not merely "a Guid on the message".
public sealed class OrganizationsTestMessage
{
    public Guid OrganizationId { get; set; }

    public Guid? ParentOrganizationId { get; set; }

    public List<Guid> AudienceOrganizationIds { get; set; } = [];

    public Guid? PartyId { get; set; }

    public Guid RoleId { get; set; }
}

internal sealed class OrganizationsTestMessagePolicyHandler : PolicyHandler
{
    public OrganizationsTestMessagePolicyHandler(Policy policy)
        : base(policy)
    {
    }

    public override Task<bool> Authorize(object message, Session session, RelationProvider relations, CancellationToken cancellationToken)
    {
        return Task.FromResult(true);
    }
}

// What the org filter asks the manager about a message it is about to read rows for (data-tenancy.md,
// The org filter): what was ASKED for, never that asking was allowed — whether a policy read
// these values is the gate's own answer (AuthorizationResult.FieldsVetted). The registration is
// the only metadata consulted, which is the whole point: a field the constraints cannot see is a
// field the filter must not read either.
public sealed class MessageOrganizationsTests
{
    static readonly Guid Hospital = Guid.NewGuid();
    static readonly Guid Wing = Guid.NewGuid();
    static readonly Guid Elsewhere = Guid.NewGuid();

    static SecurityManager Manager()
    {
        var factories = new[]
        {
            new PolicyHandlerFactory(typeof(OrganizationsTestMessage), p => new OrganizationsTestMessagePolicyHandler(p))
            {
                Fields = new Dictionary<string, RestrictAs>
                {
                    ["OrganizationId"] = RestrictAs.Organization,
                    ["ParentOrganizationId"] = RestrictAs.Organization,
                    ["AudienceOrganizationIds"] = RestrictAs.Organization,
                    ["PartyId"] = RestrictAs.Party,
                    ["RoleId"] = RestrictAs.Reference,
                },
            },
        };

        return new SecurityManager(Registry.For(factories), new RelationProvider(), factories, []);
    }

    static Guid[] NamedBy(object message)
    {
        return [.. Manager().OrganizationsNamedBy(message).Order()];
    }

    [Fact]
    public void The_organization_a_message_names_is_the_value_in_its_organization_field()
    {
        Assert.Equal([Hospital], NamedBy(new OrganizationsTestMessage { OrganizationId = Hospital }));
    }

    [Fact]
    public void The_ids_the_other_axes_carry_name_no_organization()
    {
        Assert.Empty(NamedBy(new OrganizationsTestMessage { PartyId = Elsewhere, RoleId = Wing }));
    }

    // Absent, null and Guid.Empty are one answer: whichever shape a message declares, a field
    // nobody filled names no organization and the caller's own branch is what answers.
    [Fact]
    public void An_organization_field_left_empty_names_nothing()
    {
        Assert.Empty(NamedBy(new OrganizationsTestMessage
        {
            OrganizationId = Guid.Empty,
            ParentOrganizationId = null,
            AudienceOrganizationIds = [Guid.Empty],
        }));
    }

    [Fact]
    public void Every_organization_field_the_message_carries_is_named()
    {
        var named = NamedBy(new OrganizationsTestMessage
        {
            OrganizationId = Hospital,
            ParentOrganizationId = Wing,
            AudienceOrganizationIds = [Elsewhere],
        });

        Assert.Equal<Guid[]>([.. new[] { Hospital, Wing, Elsewhere }.Order()], named);
    }

    [Fact]
    public void A_message_no_handler_is_registered_for_names_nothing()
    {
        Assert.Empty(NamedBy(new UpdateTestMessage { OrganizationId = Hospital }));
    }
}
