// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using NSail.Dates;
using NSail.Messaging.Runtime;
using NSail.Metadata;
using NSail.Security;
using NSail.Security.Annotations;

namespace NSail.Security.Tests;

// Namespace-derived keys: "Security.Tests.TestMessage", "Security.Tests.UpdateTestMessage".
public sealed class TestMessage
{
    public Guid? PartyId { get; set; }

    public Guid? OrganizationId { get; set; }

    public Guid RoleId { get; set; }

    public List<Guid> AttendeePartyIds { get; set; } = [];

    public bool Exceptional { get; set; }
}

// The verb family's odd member: an update does not carry the immutable subject, so a
// policy constraining PartyId across the family has nothing to bind to here.
public sealed class UpdateTestMessage
{
    public Guid? OrganizationId { get; set; }
}

// Hand-written mirrors of what the Policies.Handlers generator emits.
internal sealed class TestMessagePolicyHandler : PolicyHandler
{
    public TestMessagePolicyHandler(Policy policy)
        : base(policy)
    {
    }

    public override async Task<bool> Authorize(object message, Session session, RelationProvider relations, CancellationToken cancellationToken)
    {
        if (message is not TestMessage m)
            return false;

        if (!await Satisfies("PartyId", m.PartyId, RestrictAs.Party, session, relations, cancellationToken))
            return false;

        if (!await Satisfies("OrganizationId", m.OrganizationId, RestrictAs.Organization, session, relations, cancellationToken))
            return false;

        if (!await Satisfies("RoleId", m.RoleId, RestrictAs.Reference, session, relations, cancellationToken))
            return false;

        if (!await Satisfies("AttendeePartyIds", m.AttendeePartyIds, RestrictAs.Party, session, relations, cancellationToken))
            return false;

        // The flag's call names no axis and awaits nothing: the policy's own literal is the
        // whole answer, which is what the generator emits for RestrictAs.Flag.
        if (!Satisfies("Exceptional", m.Exceptional))
            return false;

        return true;
    }
}

internal sealed class UpdateTestMessagePolicyHandler : PolicyHandler
{
    public UpdateTestMessagePolicyHandler(Policy policy)
        : base(policy)
    {
    }

    public override async Task<bool> Authorize(object message, Session session, RelationProvider relations, CancellationToken cancellationToken)
    {
        if (message is not UpdateTestMessage m)
            return false;

        return await Satisfies("OrganizationId", m.OrganizationId, RestrictAs.Organization, session, relations, cancellationToken);
    }
}

// The two reports a manager makes about a stored row, captured instead of logged: a test
// that can only ask "did it throw" cannot tell a dropped key from a quarantined row, and
// those are now two different sizes of failure.
internal sealed class RecordingSecurityManager : SecurityManager
{
    public RecordingSecurityManager(
        MessageRegistry messages,
        RelationProvider relations,
        IEnumerable<PolicyHandlerFactory> factories,
        IEnumerable<Policy> builtInPolicies)
        : base(messages, relations, factories, builtInPolicies)
    {
    }

    public List<Policy> Quarantined { get; } = [];

    public List<(Policy Policy, IReadOnlyList<string> Messages, IReadOnlyList<string> Excused)> Dropped { get; } = [];

    protected override void OnQuarantined(Policy policy, Exception exception)
    {
        Quarantined.Add(policy);
    }

    protected override void OnDropped(Policy policy, IReadOnlyList<string> messages, IReadOnlyList<string> excused)
    {
        Dropped.Add((policy, messages, excused));
    }
}

internal sealed class FakeRelations : RelationProvider
{
    public bool Related { get; set; }

    /// <summary>The organizations the actor is a member of, or under one. The base answers every
    /// question true, which is right for the client and useless for a test about the org axis:
    /// a constraint that always passes proves nothing about the branch it names.</summary>
    public List<Guid> Branches { get; } = [];

    public override Task<bool> IsRelated(Guid partyId, Guid relatedPartyId, string role, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Related);
    }

    public override Task<bool> IsMember(Guid partyId, Guid organizationId, string? role, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Branches.Contains(organizationId));
    }

    public override Task<bool> IsMemberOrDescendant(Guid partyId, Guid organizationId, string? role, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Branches.Contains(organizationId));
    }
}

public sealed class SecurityManagerTests
{
    static readonly Guid Me = Guid.NewGuid();
    static readonly Guid Other = Guid.NewGuid();

    const string Key = "Security.Tests.TestMessage";
    const string UpdateKey = "Security.Tests.UpdateTestMessage";

    // A key a stored row could have been saved with and the registry cannot resolve today,
    // which is what a deleted message leaves behind.
    const string Gone = "Security.Tests.Deleted";

    static readonly Guid Hospital = Guid.NewGuid();
    static readonly Guid Wing = Guid.NewGuid();
    static readonly Guid Elsewhere = Guid.NewGuid();

    static Session Doctor(string role = "medico")
    {
        return new()
        {
            IsAuthenticated = true,
            PartyId = Me,
            Roles = [role],
        };
    }

    /// <summary>A session holding one membership and standing at it, with the chain above it
    /// already walked — which is the state the audience evaluates against, since it may not
    /// walk anything.</summary>
    static Session MemberOf(Guid organization, Guid? parent = null, string role = "medico")
    {
        var session = Doctor(role);

        session.OrganizationId = organization;

        session.Memberships =
        [
            new SessionMembership
            {
                OrganizationId = organization,
                Role = role,
                Ancestors = parent is { } id ? [id] : [],
            }
        ];

        return session;
    }

    static SecurityManager Manager(FakeRelations? relations = null, params Policy[] builtIns)
    {
        var factories = Factories();

        return new SecurityManager(Registry.For(factories), relations ?? new FakeRelations(), factories, builtIns);
    }

    static RecordingSecurityManager Recording(params Policy[] builtIns)
    {
        var factories = Factories();

        return new RecordingSecurityManager(Registry.For(factories), new FakeRelations(), factories, builtIns);
    }

    static PolicyHandlerFactory[] Factories()
    {
        return
        [
            new PolicyHandlerFactory(typeof(TestMessage), p => new TestMessagePolicyHandler(p))
            {
                Fields = new Dictionary<string, RestrictAs>
                {
                    ["PartyId"] = RestrictAs.Party,
                    ["OrganizationId"] = RestrictAs.Organization,
                    ["RoleId"] = RestrictAs.Reference,
                    ["AttendeePartyIds"] = RestrictAs.Party,
                    ["Exceptional"] = RestrictAs.Flag,
                },
            },
            new PolicyHandlerFactory(typeof(UpdateTestMessage), p => new UpdateTestMessagePolicyHandler(p))
            {
                Fields = new Dictionary<string, RestrictAs> { ["OrganizationId"] = RestrictAs.Organization },
            },
        ];
    }

    [Fact]
    public async Task Denies_by_default()
    {
        var result = await Manager().Authorize(new TestMessage(), Doctor());

        Assert.False(result.Allowed);
    }

    [Fact]
    public async Task A_flat_policy_allows_by_key_and_audience()
    {
        var manager = Manager(null, new Policy { Name = "t", Messages = [Key], Audience = new() { HasRole = "medico" } });

        Assert.True((await manager.Authorize(new TestMessage(), Doctor())).Allowed);
        Assert.False((await manager.Authorize(new TestMessage(), Doctor("enfermera"))).Allowed);
    }

    [Fact]
    public async Task A_membership_audience_takes_the_organization_it_names()
    {
        var manager = Manager(null, new Policy
        {
            Name = "t",
            Messages = [Key],
            Audience = new() { MemberOf = new() { Organization = Hospital } },
        });

        Assert.True((await manager.Authorize(new TestMessage(), MemberOf(Hospital))).Allowed);
        Assert.False((await manager.Authorize(new TestMessage(), MemberOf(Elsewhere))).Allowed);
    }

    [Fact]
    public async Task A_cascading_membership_audience_reaches_the_organizations_under_it()
    {
        var manager = Manager(null, new Policy
        {
            Name = "t",
            Messages = [Key],
            Audience = new() { MemberOf = new() { Organization = Hospital, Cascade = true } },
        });

        Assert.True((await manager.Authorize(new TestMessage(), MemberOf(Wing, Hospital))).Allowed);
        Assert.False((await manager.Authorize(new TestMessage(), MemberOf(Elsewhere))).Allowed);
    }

    [Fact]
    public async Task Without_cascade_a_membership_below_the_named_organization_does_not_qualify()
    {
        var manager = Manager(null, new Policy
        {
            Name = "t",
            Messages = [Key],
            Audience = new() { MemberOf = new() { Organization = Hospital } },
        });

        Assert.False((await manager.Authorize(new TestMessage(), MemberOf(Wing, Hospital))).Allowed);
    }

    [Fact]
    public async Task A_membership_audience_with_a_role_takes_only_that_role()
    {
        var manager = Manager(null, new Policy
        {
            Name = "t",
            Messages = [Key],
            Audience = new() { MemberOf = new() { Organization = Hospital, Role = "medico" } },
        });

        Assert.True((await manager.Authorize(new TestMessage(), MemberOf(Hospital))).Allowed);
        Assert.False((await manager.Authorize(new TestMessage(), MemberOf(Hospital, role: "enfermera"))).Allowed);
    }

    [Fact]
    public async Task A_current_organization_membership_audience_follows_the_session()
    {
        var manager = Manager(null, new Policy
        {
            Name = "t",
            Messages = [Key],
            Audience = new() { MemberOf = new() { Organization = null } },
        });

        var session = MemberOf(Hospital);
        session.OrganizationId = Hospital;

        Assert.True((await manager.Authorize(new TestMessage(), session)).Allowed);

        session.OrganizationId = Elsewhere;

        Assert.False((await manager.Authorize(new TestMessage(), session)).Allowed);
    }

    [Fact]
    public async Task A_current_organization_membership_audience_denies_without_an_active_organization()
    {
        var manager = Manager(null, new Policy
        {
            Name = "t",
            Messages = [Key],
            Audience = new() { MemberOf = new() { Organization = null } },
        });

        var session = MemberOf(Hospital);
        session.OrganizationId = null;

        Assert.False((await manager.Authorize(new TestMessage(), session)).Allowed);
    }

    // A role holds at its membership's organization and below (nsail#1582): standing
    // at the hospital, a medico held at the wing under it is not held, so neither the cascade
    // nor a literal naming the wing reaches it from there.
    [Fact]
    public async Task A_membership_below_where_the_session_stands_grants_nothing_there()
    {
        var manager = Manager(
            null,
            new Policy
            {
                Name = "cascading",
                Messages = [Key],
                Audience = new() { MemberOf = new() { Organization = null, Role = "medico", Cascade = true } },
            },
            new Policy
            {
                Name = "literal",
                Messages = [UpdateKey],
                Audience = new() { MemberOf = new() { Organization = Wing, Role = "medico" } },
            });

        var session = Standing(Hospital,
            new SessionMembership { OrganizationId = Hospital, Role = "paciente" },
            new SessionMembership { OrganizationId = Wing, Role = "medico", Ancestors = [Hospital] });

        Assert.False((await manager.Authorize(new TestMessage(), session)).Allowed);
        Assert.False((await manager.Authorize(new UpdateTestMessage(), session)).Allowed);

        session.OrganizationId = Wing;

        Assert.True((await manager.Authorize(new TestMessage(), session)).Allowed);
        Assert.True((await manager.Authorize(new UpdateTestMessage(), session)).Allowed);
    }

    [Fact]
    public async Task A_membership_above_where_the_session_stands_holds_there()
    {
        var manager = Manager(null, new Policy
        {
            Name = "t",
            Messages = [Key],
            Audience = new() { MemberOf = new() { Organization = null, Role = "cliente" } },
        });

        var session = Standing(Wing,
            new SessionMembership { OrganizationId = Wing, Role = "medico", Ancestors = [Hospital] },
            new SessionMembership { OrganizationId = Hospital, Role = "cliente" });

        Assert.True((await manager.Authorize(new TestMessage(), session)).Allowed);
    }

    [Fact]
    public async Task A_membership_at_a_sibling_grants_nothing_where_the_session_stands()
    {
        var manager = Manager(null, new Policy
        {
            Name = "t",
            Messages = [Key],
            Audience = new() { MemberOf = new() { Organization = Hospital, Role = "medico", Cascade = true } },
        });

        var session = Standing(Wing,
            new SessionMembership { OrganizationId = Wing, Role = "paciente", Ancestors = [Hospital] },
            new SessionMembership { OrganizationId = Elsewhere, Role = "medico", Ancestors = [Hospital] });

        Assert.False((await manager.Authorize(new TestMessage(), session)).Allowed);
    }

    static Session Standing(Guid organization, params SessionMembership[] memberships)
    {
        var session = Doctor();

        session.OrganizationId = organization;
        session.Memberships = [.. memberships];

        return session;
    }

    [Fact]
    public async Task A_policy_grants_only_the_messages_it_lists()
    {
        var manager = Manager(null, new Policy { Name = "t", Messages = [Key], Audience = new() { Authenticated = true } });

        Assert.True((await manager.Authorize(new TestMessage(), Doctor())).Allowed);
        Assert.False((await manager.Authorize(new UpdateTestMessage(), Doctor())).Allowed);
    }

    [Fact]
    public async Task One_policy_covers_a_verb_family()
    {
        var manager = Manager(null, new Policy
        {
            Name = "doctors manage their own",
            Messages = [Key, UpdateKey],
            Audience = new() { HasRole = "medico" },
        });

        Assert.True((await manager.Authorize(new TestMessage(), Doctor())).Allowed);
        Assert.True((await manager.Authorize(new UpdateTestMessage(), Doctor())).Allowed);
    }

    [Fact]
    public async Task A_shared_constraint_binds_where_the_field_exists_and_is_silent_where_it_does_not()
    {
        var manager = Manager(null, new Policy
        {
            Name = "doctors manage their own patients",
            Messages = [Key, UpdateKey],
            Audience = new() { HasRole = "medico" },
            Fields = new Dictionary<string, PolicyConstraint> { ["PartyId"] = PolicyConstraint.Me },
        });

        // The member that carries the field is constrained by it.
        Assert.True((await manager.Authorize(new TestMessage { PartyId = Me }, Doctor())).Allowed);
        Assert.False((await manager.Authorize(new TestMessage { PartyId = Other }, Doctor())).Allowed);

        // The member that does not carry it is allowed: the subject is immutable there, so
        // there is nothing to lie about — this is the lenient rule, not a hole.
        Assert.True((await manager.Authorize(new UpdateTestMessage(), Doctor())).Allowed);
    }

    [Fact]
    public async Task A_constraint_no_member_declares_is_a_dead_constraint()
    {
        var policy = new Policy
        {
            Name = "typo",
            Messages = [UpdateKey],
            Fields = new Dictionary<string, PolicyConstraint> { ["PartyId"] = PolicyConstraint.Me },
        };

        var error = Assert.Throws<InvalidOperationException>(() => Manager(null, policy));

        Assert.Contains("PartyId", error.Message);

        await Task.CompletedTask;
    }

    [Fact]
    public async Task Wildcards_expand_against_the_registry()
    {
        var root = Manager(null, new Policy { Name = "root", Messages = ["*"], Audience = new() { HasRole = "admin" } });
        var admin = Doctor("admin");

        // Every registered message, not a pattern kept around for evaluation.
        Assert.True((await root.Authorize(new TestMessage(), admin)).Allowed);
        Assert.True((await root.Authorize(new UpdateTestMessage(), admin)).Allowed);
        Assert.False((await root.Authorize(new TestMessage(), Doctor())).Allowed);
    }

    [Fact]
    public async Task A_feature_wildcard_expands_to_its_feature()
    {
        var manager = Manager(null, new Policy { Name = "feature", Messages = ["Security.Tests.*"], Audience = new() { Authenticated = true } });

        Assert.True((await manager.Authorize(new TestMessage(), Doctor())).Allowed);
        Assert.True((await manager.Authorize(new UpdateTestMessage(), Doctor())).Allowed);
    }

    [Fact]
    public async Task Overlapping_patterns_hydrate_a_message_once()
    {
        var manager = Manager(null, new Policy { Name = "both", Messages = ["*", Key], Audience = new() { Authenticated = true } });

        var result = await manager.Authorize(new TestMessage(), Doctor());

        Assert.True(result.Allowed);
        Assert.Single(result.Matches);
    }

    [Fact]
    public async Task An_audience_less_policy_is_anonymous()
    {
        var manager = Manager(null,
            new Policy { Name = "anonymous", Messages = [Key] },
            new Policy { Name = "authenticated", Messages = ["*"], Audience = new() { Authenticated = true } });

        var anonymous = new Session();

        Assert.True((await manager.Authorize(new TestMessage(), anonymous)).Allowed);

        var byName = (await manager.Authorize(new TestMessage(), anonymous)).Matches.Single();
        Assert.Equal("anonymous", byName.Policy.Name);
    }

    [Fact]
    public async Task The_authenticated_audience_takes_any_session_and_no_anonymous_one()
    {
        var manager = Manager(null, new Policy
        {
            Name = "authenticated users change their own password",
            Messages = [Key],
            Audience = new PolicyAudience { Authenticated = true },
        });

        // Any session at all: this is the audience self-service needs, where naming a role
        // or a party would be exactly wrong.
        Assert.True((await manager.Authorize(new TestMessage(), Doctor())).Allowed);
        Assert.True((await manager.Authorize(new TestMessage(), Doctor("recepcionista"))).Allowed);

        Assert.False((await manager.Authorize(new TestMessage(), new Session())).Allowed);
    }

    [Fact]
    public async Task Me_locks_the_field_to_the_session_party()
    {
        var manager = Manager(null, new Policy
        {
            Name = "own",
            Messages = [Key],
            Audience = new() { HasRole = "medico" },
            Fields = new Dictionary<string, PolicyConstraint> { ["PartyId"] = PolicyConstraint.Me },
        });

        Assert.True((await manager.Authorize(new TestMessage { PartyId = Me }, Doctor())).Allowed);
        Assert.False((await manager.Authorize(new TestMessage { PartyId = Other }, Doctor())).Allowed);
        Assert.False((await manager.Authorize(new TestMessage(), Doctor())).Allowed);
    }

    [Fact]
    public async Task Literal_arrays_are_contains()
    {
        var allowed = Guid.NewGuid();
        var manager = Manager(null, new Policy
        {
            Name = "pinned",
            Messages = [Key],
            Audience = new() { Authenticated = true },
            Fields = new Dictionary<string, PolicyConstraint> { ["OrganizationId"] = PolicyConstraint.Literal(allowed) },
        });

        Assert.True((await manager.Authorize(new TestMessage { OrganizationId = allowed }, Doctor())).Allowed);
        Assert.False((await manager.Authorize(new TestMessage { OrganizationId = Other }, Doctor())).Allowed);
    }

    // Leonardo's named case, in the evaluator: which roles a caller may hand out. The
    // message gate has always said who may create a membership; until Reference existed,
    // nothing said which role they could put in it.
    [Fact]
    public async Task A_reference_allows_only_the_ids_the_policy_lists()
    {
        var assignable = Guid.NewGuid();
        var administrator = Guid.NewGuid();

        var manager = Manager(null, new Policy
        {
            Name = "managers enrol sellers",
            Messages = [Key],
            Audience = new() { HasRole = "medico" },
            Fields = new Dictionary<string, PolicyConstraint> { ["RoleId"] = PolicyConstraint.Literal(assignable) },
        });

        Assert.True((await manager.Authorize(new TestMessage { RoleId = assignable }, Doctor())).Allowed);
        Assert.False((await manager.Authorize(new TestMessage { RoleId = administrator }, Doctor())).Allowed);
    }

    // Literal-only is enforced where it can still be fixed, not left to a denial nobody
    // authored: the same question the save door asks, asked again at hydration.
    [Fact]
    public void A_symbol_on_a_reference_field_is_refused()
    {
        var policy = new Policy
        {
            Name = "unauthorable",
            Messages = [Key],
            Audience = new() { HasRole = "medico" },
            Fields = new Dictionary<string, PolicyConstraint> { ["RoleId"] = PolicyConstraint.Me },
        };

        Assert.Throws<InvalidOperationException>(() => Manager(null, policy));

        var validation = Manager().ValidateDocument(policy);

        Assert.Equal("SymbolOnReference", Assert.Single(validation.Errors).Code);
    }

    // The fourth axis, and the case it was founded for: an override anybody can tick is a
    // record, not a control. The policy pins the flag and the send has to carry that value —
    // no screen, no warning, a refusal.
    [Fact]
    public async Task A_flag_pinned_to_false_refuses_the_send_that_carries_true()
    {
        var manager = Manager(null, Pinning(PolicyConstraint.Literal(false)));

        Assert.True((await manager.Authorize(new TestMessage(), Doctor())).Allowed);
        Assert.False((await manager.Authorize(new TestMessage { Exceptional = true }, Doctor())).Allowed);
    }

    // Both directions, because the axis is a value and not a permission: a row pinning true
    // is authorable and means the caller may only send it set.
    [Fact]
    public async Task A_flag_pinned_to_true_refuses_the_send_that_carries_false()
    {
        var manager = Manager(null, Pinning(PolicyConstraint.Literal(true)));

        Assert.True((await manager.Authorize(new TestMessage { Exceptional = true }, Doctor())).Allowed);
        Assert.False((await manager.Authorize(new TestMessage(), Doctor())).Allowed);
    }

    // Lenient fields, on the new axis: a policy that says nothing about the flag leaves it
    // free, which is what keeps every row authored before the axis existed granting exactly
    // what it granted. "*" is the same answer said out loud.
    [Fact]
    public async Task A_flag_no_policy_constrains_stays_free()
    {
        var silent = Manager(null, new Policy { Name = "flat", Messages = [Key], Audience = new() { HasRole = "medico" } });
        var any = Manager(null, Pinning(PolicyConstraint.Any));

        Assert.True((await silent.Authorize(new TestMessage { Exceptional = true }, Doctor())).Allowed);
        Assert.True((await silent.Authorize(new TestMessage(), Doctor())).Allowed);
        Assert.True((await any.Authorize(new TestMessage { Exceptional = true }, Doctor())).Allowed);
        Assert.True((await any.Authorize(new TestMessage(), Doctor())).Allowed);
    }

    // Literal-only, the same word Reference carries and enforced at the same two doors: no
    // symbol relates a flag to the actor, so one on this axis is refused where it can still
    // be fixed and again for the row that arrived some other way.
    [Fact]
    public void A_symbol_on_a_flag_field_is_refused()
    {
        var policy = Pinning(PolicyConstraint.Me);

        Assert.Throws<InvalidOperationException>(() => Manager(null, policy));
        Assert.Equal("SymbolOnFlag", Assert.Single(Manager().ValidateDocument(policy).Errors).Code);
    }

    [Fact]
    public void A_list_of_ids_on_a_flag_field_is_refused()
    {
        var policy = Pinning(PolicyConstraint.Literal(Guid.NewGuid()));

        Assert.Throws<InvalidOperationException>(() => Manager(null, policy));
        Assert.Equal("ValuesOnFlag", Assert.Single(Manager().ValidateDocument(policy).Errors).Code);
    }

    // The other direction of the same mismatch: true is not an id, so a flag on an axis that
    // names values could only ever deny.
    [Fact]
    public void A_flag_on_a_field_that_names_values_is_refused()
    {
        var policy = new Policy
        {
            Name = "unauthorable",
            Messages = [Key],
            Audience = new() { HasRole = "medico" },
            Fields = new Dictionary<string, PolicyConstraint> { ["PartyId"] = PolicyConstraint.Literal(true) },
        };

        Assert.Throws<InvalidOperationException>(() => Manager(null, policy));
        Assert.Equal("FlagOnValue", Assert.Single(Manager().ValidateDocument(policy).Errors).Code);
    }

    // The client's half of the gate: a flag is a literal the browser can decide, so the
    // control is not drawn instead of being drawn and refused. It answers no when nothing
    // could ever satisfy the value — including when no policy covers the message at all.
    [Fact]
    public void Permits_answers_what_the_pinned_flag_leaves_possible()
    {
        Assert.False(Manager(null, Pinning(PolicyConstraint.Literal(false))).Permits(typeof(TestMessage), "Exceptional", true, Doctor()));
        Assert.True(Manager(null, Pinning(PolicyConstraint.Literal(false))).Permits(typeof(TestMessage), "Exceptional", false, Doctor()));
        Assert.True(Manager(null, Pinning(PolicyConstraint.Literal(true))).Permits(typeof(TestMessage), "Exceptional", true, Doctor()));
        Assert.True(Manager(null, Pinning(PolicyConstraint.Any)).Permits(typeof(TestMessage), "Exceptional", true, Doctor()));
        Assert.False(Manager().Permits(typeof(TestMessage), "Exceptional", true, Doctor()));
    }

    // Policies OR, and so does this: the row that pins the flag shut cannot take away what a
    // second row the caller also answers to leaves free.
    [Fact]
    public async Task A_second_policy_leaving_the_flag_free_allows_what_the_pinned_one_refuses()
    {
        var manager = Manager(
            null,
            Pinning(PolicyConstraint.Literal(false)),
            new Policy { Name = "supervisor", Messages = [Key], Audience = new() { HasRole = "medico" } });

        Assert.True((await manager.Authorize(new TestMessage { Exceptional = true }, Doctor())).Allowed);
        Assert.True(manager.Permits(typeof(TestMessage), "Exceptional", true, Doctor()));
    }

    static Policy Pinning(PolicyConstraint constraint)
    {
        return new Policy
        {
            Name = "the exception is a grant",
            Messages = [Key],
            Audience = new() { HasRole = "medico" },
            Fields = new Dictionary<string, PolicyConstraint> { ["Exceptional"] = constraint },
        };
    }

    // Universal quantification: the constraint holds for every element or the message is
    // denied. One foreign attendee is what makes the whole booking someone else's.
    [Fact]
    public async Task A_collection_denies_when_one_element_falls_outside_the_constraint()
    {
        var manager = Manager(null, new Policy
        {
            Name = "own appointments only",
            Messages = [Key],
            Audience = new() { HasRole = "medico" },
            Fields = new Dictionary<string, PolicyConstraint> { ["AttendeePartyIds"] = PolicyConstraint.Me },
        });

        Assert.True((await manager.Authorize(new TestMessage { AttendeePartyIds = [Me] }, Doctor())).Allowed);
        Assert.True((await manager.Authorize(new TestMessage { AttendeePartyIds = [Me, Me] }, Doctor())).Allowed);
        Assert.False((await manager.Authorize(new TestMessage { AttendeePartyIds = [Me, Other] }, Doctor())).Allowed);
        Assert.False((await manager.Authorize(new TestMessage { AttendeePartyIds = [Other] }, Doctor())).Allowed);
    }

    // The org axis's own reading of an omitted claim (nsail#1702). A branch the caller does not
    // stand in is refused, which is what arms the axis; naming NO branch is not a widening at all
    // — the row filter scopes a message that names none to the session's own subtree (data.md,
    // The org filter), which is inside what the symbol permits. Refusing it would refuse every
    // by-id read and every transition in the tree.
    [Fact]
    public async Task A_membership_symbol_refuses_another_branch_and_allows_no_branch()
    {
        var relations = new FakeRelations();

        relations.Branches.Add(Hospital);

        var manager = Manager(relations, new Policy
        {
            Name = "own branch only",
            Messages = [Key],
            Audience = new() { HasRole = "medico" },
            Fields = new Dictionary<string, PolicyConstraint>
            {
                ["OrganizationId"] = PolicyConstraint.MemberOfOrDescendant,
            },
        });

        Assert.True((await manager.Authorize(new TestMessage { OrganizationId = Hospital }, Doctor())).Allowed);
        Assert.False((await manager.Authorize(new TestMessage { OrganizationId = Elsewhere }, Doctor())).Allowed);
        Assert.True((await manager.Authorize(new TestMessage(), Doctor())).Allowed);
    }

    // The two org symbols the tolerance does NOT reach. What makes the omission safe above is
    // that the filter's fallback — the seat PLUS everything under it — is inside
    // @memberOfOrDescendant's own set. @current permits the seat alone and @memberOf the seat's
    // memberships without descending, so tolerating null there would hand back a subtree that
    // naming any branch of it is refused for: widening by omission, which is the one thing the
    // null rejection exists to stop.
    [Fact]
    public async Task The_narrower_org_symbols_still_refuse_no_branch()
    {
        var relations = new FakeRelations();

        relations.Branches.Add(Hospital);

        foreach (var constraint in new[] { PolicyConstraint.Current, PolicyConstraint.MemberOf })
        {
            var manager = Manager(relations, new Policy
            {
                Name = "the seat and nothing below it",
                Messages = [Key],
                Audience = new() { HasRole = "medico" },
                Fields = new Dictionary<string, PolicyConstraint> { ["OrganizationId"] = constraint },
            });

            Assert.True((await manager.Authorize(new TestMessage { OrganizationId = Hospital }, Standing())).Allowed);
            Assert.False((await manager.Authorize(new TestMessage(), Standing())).Allowed);
        }
    }

    // A seat, which is what @current resolves against and what the row filter would descend from.
    static Session Standing()
    {
        var session = Doctor();

        session.OrganizationId = Hospital;

        return session;
    }

    // And the line the tolerance stops at: a literal set names branches the author chose, the
    // seat is not one of them, and nothing behind the gate would narrow an omission to it.
    [Fact]
    public async Task A_literal_branch_list_still_refuses_no_branch()
    {
        var manager = Manager(null, new Policy
        {
            Name = "one branch by id",
            Messages = [Key],
            Audience = new() { HasRole = "medico" },
            Fields = new Dictionary<string, PolicyConstraint>
            {
                ["OrganizationId"] = PolicyConstraint.Literal(Hospital),
            },
        });

        Assert.True((await manager.Authorize(new TestMessage { OrganizationId = Hospital }, Doctor())).Allowed);
        Assert.False((await manager.Authorize(new TestMessage(), Doctor())).Allowed);
    }

    // The party axis keeps the rejection whole: @me has no second door behind it, so a caller who
    // may read only their own rows has to say whose rows they are asking for.
    [Fact]
    public async Task The_party_axis_still_refuses_an_omitted_claim()
    {
        var manager = Manager(null, new Policy
        {
            Name = "own rows only",
            Messages = [Key],
            Audience = new() { HasRole = "medico" },
            Fields = new Dictionary<string, PolicyConstraint> { ["PartyId"] = PolicyConstraint.Me },
        });

        Assert.True((await manager.Authorize(new TestMessage { PartyId = Me }, Doctor())).Allowed);
        Assert.False((await manager.Authorize(new TestMessage(), Doctor())).Allowed);
    }

    // The empty list is the collection's null. Quantified over nothing the constraint would
    // pass vacuously, which is a constrained field widening by omission — the one thing the
    // scalar's null rejection exists to stop.
    [Fact]
    public async Task An_empty_collection_is_refused_like_a_null_scalar()
    {
        var manager = Manager(null, new Policy
        {
            Name = "own appointments only",
            Messages = [Key],
            Audience = new() { HasRole = "medico" },
            Fields = new Dictionary<string, PolicyConstraint> { ["AttendeePartyIds"] = PolicyConstraint.Me },
        });

        Assert.False((await manager.Authorize(new TestMessage(), Doctor())).Allowed);
    }

    // A collection is a shape, not an axis: every kind wears it, and the elements answer to
    // the kind the field was marked with.
    [Fact]
    public async Task A_collection_asks_the_graph_once_per_element()
    {
        var relations = new FakeRelations();
        var manager = Manager(relations, new Policy
        {
            Name = "patients",
            Messages = [Key],
            Audience = new() { HasRole = "medico" },
            Fields = new Dictionary<string, PolicyConstraint> { ["AttendeePartyIds"] = PolicyConstraint.RelatedAs("medico") },
        });

        relations.Related = true;
        Assert.True((await manager.Authorize(new TestMessage { AttendeePartyIds = [Other] }, Doctor())).Allowed);

        relations.Related = false;
        Assert.False((await manager.Authorize(new TestMessage { AttendeePartyIds = [Other] }, Doctor())).Allowed);
    }

    // An unconstrained collection is free, exactly like an unconstrained scalar: marking the
    // field must not make it required for callers no policy narrows.
    [Fact]
    public async Task An_unconstrained_collection_stays_free()
    {
        var manager = Manager(null, new Policy
        {
            Name = "any appointment",
            Messages = [Key],
            Audience = new() { HasRole = "medico" },
        });

        Assert.True((await manager.Authorize(new TestMessage(), Doctor())).Allowed);
        Assert.True((await manager.Authorize(new TestMessage { AttendeePartyIds = [Other] }, Doctor())).Allowed);
    }

    [Fact]
    public async Task RelatedAs_asks_the_graph_provider()
    {
        var relations = new FakeRelations();
        var manager = Manager(relations, new Policy
        {
            Name = "patients",
            Messages = [Key],
            Audience = new() { HasRole = "medico" },
            Fields = new Dictionary<string, PolicyConstraint> { ["PartyId"] = PolicyConstraint.RelatedAs("medico") },
        });

        relations.Related = true;
        Assert.True((await manager.Authorize(new TestMessage { PartyId = Other }, Doctor())).Allowed);

        relations.Related = false;
        Assert.False((await manager.Authorize(new TestMessage { PartyId = Other }, Doctor())).Allowed);
    }

    [Fact]
    public async Task Policies_OR_and_do_not_merge_fields()
    {
        var manager = Manager(null,
            new Policy
            {
                Name = "doctors see their own",
                Messages = [Key],
                Audience = new() { HasRole = "medico" },
                Fields = new Dictionary<string, PolicyConstraint> { ["PartyId"] = PolicyConstraint.Me },
            },
            new Policy { Name = "admins see everything", Messages = [Key], Audience = new() { HasRole = "admin" } });

        var doctorAdmin = new Session { IsAuthenticated = true, PartyId = Me, Roles = ["medico", "admin"] };

        var result = await manager.Authorize(new TestMessage { PartyId = null }, doctorAdmin);

        Assert.True(result.Allowed);
        Assert.Equal("admins see everything", result.Matches.Single().Policy.Name);
    }

    [Fact]
    public async Task Validity_windows_apply()
    {
        var today = BusinessDate.Today;
        var manager = Manager(null,
            new Policy { Name = "expired", Messages = [Key], ValidTo = today.AddDays(-1) },
            new Policy { Name = "future", Messages = [Key], ValidFrom = today.AddDays(1) });

        Assert.False((await manager.Authorize(new TestMessage(), Doctor())).Allowed);
    }

    [Fact]
    public async Task The_system_session_bypasses_the_gate()
    {
        Assert.True((await Manager().Authorize(new TestMessage(), Session.System())).Allowed);
    }

    // Hydration's half of the wildcard rule: the pattern is expanded first, so the constraint
    // binds against what it covers and only a field nothing there declares is still a typo.
    [Fact]
    public void Field_constraints_on_wildcards_bind_against_the_expansion()
    {
        var policy = new Policy
        {
            Name = "wide",
            Messages = ["Security.*"],
            Fields = new Dictionary<string, PolicyConstraint> { ["PartyId"] = PolicyConstraint.Me },
        };

        Assert.Equal("wide", Assert.Single(Manager(null, policy).BuiltInPolicies).Name);
    }

    // The editor's half of the same rule. A panel that offered nothing for a feature grant would
    // hide a constraint the document carries, and the next save through the editor would drop it.
    [Fact]
    public void The_constrainable_fields_of_a_wildcard_are_the_fields_of_what_it_covers()
    {
        var fields = Manager().GetConstrainableFields(["Security.*"]);

        Assert.Equal(RestrictAs.Organization, fields["OrganizationId"]);
        Assert.Equal(RestrictAs.Party, fields["PartyId"]);
        Assert.Equal(RestrictAs.Flag, fields["Exceptional"]);
    }

    [Fact]
    public void A_field_no_message_the_wildcard_covers_declares_is_rejected()
    {
        var policy = new Policy
        {
            Name = "invalid",
            Messages = ["Security.*"],
            Fields = new Dictionary<string, PolicyConstraint> { ["Nowhere"] = PolicyConstraint.Me },
        };

        Assert.Throws<InvalidOperationException>(() => Manager(null, policy));
    }

    [Fact]
    public void An_unregistered_message_is_rejected()
    {
        var policy = new Policy { Name = "orphan", Messages = ["Nowhere.Feature.Unknown"] };

        Assert.Throws<InvalidOperationException>(() => Manager(null, policy));
    }

    [Fact]
    public void A_policy_must_list_a_message()
    {
        Assert.Throws<InvalidOperationException>(() => Manager(null, new Policy { Name = "empty", Messages = [] }));
    }

    [Fact]
    public void Effective_policies_keep_the_audiences_that_match_and_drop_the_rest()
    {
        var mine = new Policy { Name = "mine", Messages = ["*"], Audience = new PolicyAudience { HasRole = "medico" } };
        var other = new Policy { Name = "other", Messages = ["*"], Audience = new PolicyAudience { HasRole = "admin" } };
        var anonymous = new Policy { Name = "anonymous", Messages = [Key] };

        var effective = Manager(null, mine, other, anonymous).GetEffectivePolicies(Doctor());

        Assert.Equal(["mine", "anonymous"], effective.Select(policy => policy.Name));
    }

    [Fact]
    public void An_expanded_policy_is_reported_once()
    {
        var root = new Policy { Name = "root", Messages = ["*"], Audience = new PolicyAudience { Authenticated = true } };

        var effective = Manager(null, root).GetEffectivePolicies(Doctor());

        Assert.Equal(["root"], effective.Select(policy => policy.Name));
    }

    [Fact]
    public void Effective_policies_ignore_the_message_key_but_not_the_validity_period()
    {
        var expired = new Policy
        {
            Name = "expired",
            Messages = ["*"],
            Audience = new PolicyAudience { HasRole = "medico" },
            ValidTo = BusinessDate.Today.AddDays(-1),
        };

        var effective = Manager(null, expired).GetEffectivePolicies(Doctor());

        Assert.Empty(effective);
    }

    [Fact]
    public void An_anonymous_session_gets_only_the_audienceless_policies()
    {
        var authenticated = new Policy { Name = "authenticated", Messages = ["*"], Audience = new PolicyAudience { Authenticated = true } };
        var anonymous = new Policy { Name = "anonymous", Messages = [Key] };

        var effective = Manager(null, authenticated, anonymous).GetEffectivePolicies(new Session());

        Assert.Equal(["anonymous"], effective.Select(policy => policy.Name));
    }

    [Fact]
    public void A_valid_document_passes_validation()
    {
        var validation = Manager().ValidateDocument(new Policy
        {
            Name = "ok",
            Messages = [Key, UpdateKey],
            Audience = new PolicyAudience { HasRole = "medico" },
            Fields = new Dictionary<string, PolicyConstraint> { ["PartyId"] = PolicyConstraint.Me },
        });

        Assert.True(validation.IsValid);
        Assert.False(validation.IsAnonymous);
    }

    [Fact]
    public void Validation_rejects_a_constraint_no_member_declares()
    {
        var validation = Manager().ValidateDocument(new Policy
        {
            Name = "typo",
            Messages = [UpdateKey],
            Fields = new Dictionary<string, PolicyConstraint> { ["PartyId"] = PolicyConstraint.Me },
        });

        var error = Assert.Single(validation.Errors);

        Assert.Equal("UnboundConstraint", error.Code);
        Assert.Equal("PartyId", error.Source);
    }

    // A feature grant may carry a constraint: the pattern is expanded before the field is bound,
    // so the check is against the messages it covers — and a message that joins the pattern later
    // can only ever arrive narrowed, which is the direction this model fails in.
    [Fact]
    public void Validation_accepts_fields_on_a_wildcard_the_expansion_declares()
    {
        var validation = Manager().ValidateDocument(new Policy
        {
            Name = "wide",
            Messages = ["*"],
            Fields = new Dictionary<string, PolicyConstraint> { ["PartyId"] = PolicyConstraint.Me },
        });

        Assert.Empty(validation.Errors);
    }

    [Fact]
    public void Validation_rejects_a_field_no_message_the_wildcard_covers_declares()
    {
        var validation = Manager().ValidateDocument(new Policy
        {
            Name = "wide",
            Messages = ["*"],
            Fields = new Dictionary<string, PolicyConstraint> { ["Nowhere"] = PolicyConstraint.Me },
        });

        var error = Assert.Single(validation.Errors);

        Assert.Equal("UnboundConstraint", error.Code);
        Assert.Equal("Nowhere", error.Source);
    }

    // The evaluator's half of the same sentence: the expansion hydrates one handler per covered
    // message and each one binds the constraint, so a wildcard grant narrows every member.
    [Fact]
    public async Task A_wildcard_grant_binds_its_constraint_to_every_message_it_covers()
    {
        var manager = Manager(builtIns: new Policy
        {
            Name = "wide",
            Messages = ["*"],
            Audience = new PolicyAudience { Authenticated = true },
            Fields = new Dictionary<string, PolicyConstraint> { ["OrganizationId"] = PolicyConstraint.Current },
        });

        var doctor = Doctor();
        doctor.OrganizationId = Hospital;

        Assert.True((await manager.Authorize(new TestMessage { OrganizationId = Hospital }, doctor)).Allowed);
        Assert.True((await manager.Authorize(new UpdateTestMessage { OrganizationId = Hospital }, doctor)).Allowed);
        Assert.False((await manager.Authorize(new UpdateTestMessage { OrganizationId = Elsewhere }, doctor)).Allowed);
    }

    [Fact]
    public void Validation_rejects_an_unregistered_message()
    {
        var validation = Manager().ValidateDocument(new Policy { Name = "orphan", Messages = ["Nowhere.Feature.Unknown"] });

        Assert.Equal("UnknownMessage", Assert.Single(validation.Errors).Code);
    }

    [Fact]
    public void Validation_flags_an_audienceless_policy_without_refusing_it()
    {
        var validation = Manager().ValidateDocument(new Policy { Name = "anonymous", Messages = [Key] });

        // Legal, and one step from an unauthenticated API: the editor warns, it does not block.
        Assert.True(validation.IsValid);
        Assert.True(validation.IsAnonymous);
    }

    [Fact]
    public void Validation_rejects_a_document_with_no_messages()
    {
        var validation = Manager().ValidateDocument(new Policy { Name = "empty", Messages = [] });

        Assert.Equal("PolicyWithoutMessages", Assert.Single(validation.Errors).Code);
    }

    [Theory]
    [InlineData("*", "Iam.Authentication.SignIn", true)]
    [InlineData("Iam.Authentication.*", "Iam.Authentication.SignIn", true)]
    [InlineData("Iam.Authentication.*", "Directory.Parties.GetParty", false)]
    [InlineData("Iam.Authentication.SignIn", "Iam.Authentication.SignIn", true)]
    [InlineData("Iam.Authentication.SignIn", "Iam.Authentication.SignOut", false)]
    public void Key_patterns_match(string pattern, string key, bool expected)
    {
        Assert.Equal(expected, PolicyHandler.MatchesKey(pattern, key));
    }

    [Fact]
    public void RequiresMe_is_true_when_the_only_matching_policy_locks_the_field_to_me()
    {
        var manager = Manager(null, new Policy
        {
            Name = "own",
            Messages = [Key],
            Audience = new() { HasRole = "medico" },
            Fields = new Dictionary<string, PolicyConstraint> { ["PartyId"] = PolicyConstraint.Me },
        });

        Assert.True(manager.RequiresMe(typeof(TestMessage), "PartyId", Doctor()));
    }

    [Fact]
    public void RequiresMe_is_false_when_a_matching_policy_leaves_the_field_free()
    {
        // The staff shape: a flat policy with no Fields at all grants the unfiltered send,
        // so the field's satisfiable set is not "me alone" and the page must not default it.
        var manager = Manager(null, new Policy { Name = "staff", Messages = [Key], Audience = new() { HasRole = "medico" } });

        Assert.False(manager.RequiresMe(typeof(TestMessage), "PartyId", Doctor()));
    }

    [Fact]
    public void RequiresMe_is_false_when_any_matching_policy_ORs_in_a_free_view()
    {
        // Doctor+admin holding both a Me-constrained row and a flat one: OR composes, so the
        // unfiltered send still passes through the admin row and the page must not lock it.
        var manager = Manager(null,
            new Policy
            {
                Name = "own",
                Messages = [Key],
                Audience = new() { HasRole = "medico" },
                Fields = new Dictionary<string, PolicyConstraint> { ["PartyId"] = PolicyConstraint.Me },
            },
            new Policy { Name = "admin", Messages = [Key], Audience = new() { HasRole = "admin" } });

        var doctorAdmin = new Session { IsAuthenticated = true, PartyId = Me, Roles = ["medico", "admin"] };

        Assert.False(manager.RequiresMe(typeof(TestMessage), "PartyId", doctorAdmin));
    }

    [Fact]
    public void RequiresMe_is_false_when_no_policy_matches_the_session_at_all()
    {
        var manager = Manager(null, new Policy
        {
            Name = "own",
            Messages = [Key],
            Audience = new() { HasRole = "medico" },
            Fields = new Dictionary<string, PolicyConstraint> { ["PartyId"] = PolicyConstraint.Me },
        });

        Assert.False(manager.RequiresMe(typeof(TestMessage), "PartyId", Doctor("enfermera")));
    }

    [Fact]
    public void RequiresMe_is_false_for_a_system_session()
    {
        var manager = Manager(null, new Policy
        {
            Name = "own",
            Messages = [Key],
            Audience = new() { HasRole = "medico" },
            Fields = new Dictionary<string, PolicyConstraint> { ["PartyId"] = PolicyConstraint.Me },
        });

        Assert.False(manager.RequiresMe(typeof(TestMessage), "PartyId", Session.System()));
    }

    [Fact]
    public void LoadPolicies_quarantines_a_row_that_will_not_hydrate_without_losing_the_rest()
    {
        var manager = Manager();
        var good = new Policy { Name = "good", Messages = [Key], Audience = new() { Authenticated = true } };

        var exception = Record.Exception(() => manager.LoadPolicies([Broken(), good]));

        Assert.Null(exception);
    }

    [Fact]
    public async Task A_quarantined_row_grants_nothing_but_a_sibling_row_still_authorizes()
    {
        var manager = Manager();
        var good = new Policy { Name = "good", Messages = [Key], Audience = new() { Authenticated = true } };

        manager.LoadPolicies([Broken(), good]);

        Assert.True((await manager.Authorize(new TestMessage(), Doctor())).Allowed);
    }

    // nsail#1453. A row written against the registry of its own day outlives it, and a
    // message deleted afterwards used to quarantine the whole row: the blast radius was a
    // role, and what the operator saw was a screen that failed with no reason on it.
    [Fact]
    public async Task A_message_the_registry_no_longer_knows_costs_its_own_grant_and_nothing_else()
    {
        var manager = Recording();
        var stale = new Policy { Id = Guid.NewGuid(), Name = "stale", Messages = [Gone, Key], Audience = new() { Authenticated = true } };

        manager.LoadPolicies([stale]);

        Assert.True((await manager.Authorize(new TestMessage(), Doctor())).Allowed);
        Assert.Empty(manager.Quarantined);
        Assert.Equal([Gone], Assert.Single(manager.Dropped).Messages);
    }

    [Fact]
    public async Task A_row_whose_every_message_is_gone_grants_nothing_and_is_still_not_quarantined()
    {
        var manager = Recording();
        var stale = new Policy { Id = Guid.NewGuid(), Name = "stale", Messages = [Gone], Audience = new() { Authenticated = true } };

        manager.LoadPolicies([stale]);

        Assert.False((await manager.Authorize(new TestMessage(), Doctor())).Allowed);
        Assert.Empty(manager.Quarantined);
        Assert.Equal([Gone], Assert.Single(manager.Dropped).Messages);
    }

    // The drop leaves PartyId declared by nobody — UpdateTestMessage does not carry it — and
    // that is the unbound-constraint error, reached here by the drop rather than by a typo.
    // A constraint no surviving member declares is a no-op under lenient fields, so failing
    // the row for it would undo the whole point of dropping the key.
    [Fact]
    public async Task A_constraint_left_unbound_by_the_drop_does_not_quarantine_the_row()
    {
        var manager = Recording();

        var stale = new Policy
        {
            Id = Guid.NewGuid(),
            Name = "stale",
            Messages = [Gone, UpdateKey],
            Audience = new() { Authenticated = true },
            Fields = new Dictionary<string, PolicyConstraint> { ["PartyId"] = PolicyConstraint.Me },
        };

        manager.LoadPolicies([stale]);

        Assert.Empty(manager.Quarantined);
        Assert.True((await manager.Authorize(new UpdateTestMessage(), Doctor())).Allowed);
        Assert.Equal(["PartyId"], Assert.Single(manager.Dropped).Excused);
    }

    // The excuse is the row's and not the dropped key's, because nothing can make it the
    // key's: a deleted message takes its [PolicyField] declarations with it, so "did THAT
    // key declare this field" has no answer, and the answerable question — does a LIVE
    // message declare it — quarantines the row whenever the dead message owned the name
    // alone, which is the case the drop exists for. So a plain typo, declared by nothing
    // anywhere and unrelated to the deletion, rides out on the first dead key its row
    // carries. It keeps its grant — the constraint bound nothing to begin with — and the
    // report is the whole of what stands between a mis-typed constraint and nobody ever
    // hearing about it.
    [Fact]
    public async Task A_constraint_declared_by_no_message_at_all_rides_the_drop_and_is_reported()
    {
        var manager = Recording();

        var stale = new Policy
        {
            Id = Guid.NewGuid(),
            Name = "stale",
            Messages = [Gone, Key],
            Audience = new() { Authenticated = true },
            Fields = new Dictionary<string, PolicyConstraint> { ["TotallyBogusField"] = PolicyConstraint.Me },
        };

        manager.LoadPolicies([stale]);

        Assert.Empty(manager.Quarantined);
        Assert.True((await manager.Authorize(new TestMessage(), Doctor())).Allowed);

        var report = Assert.Single(manager.Dropped);

        Assert.Equal([Gone], report.Messages);
        Assert.Equal(["TotallyBogusField"], report.Excused);
    }

    // Nothing excused, nothing said about it: the drop report carries the amnesty and only
    // the amnesty, so a stale row whose surviving members declare everything it constrains
    // does not print a second line for an empty list.
    [Fact]
    public void A_drop_that_leaves_every_constraint_bound_reports_no_amnesty()
    {
        var manager = Recording();

        var stale = new Policy
        {
            Id = Guid.NewGuid(),
            Name = "stale",
            Messages = [Gone, Key],
            Audience = new() { Authenticated = true },
            Fields = new Dictionary<string, PolicyConstraint> { ["PartyId"] = PolicyConstraint.Me },
        };

        manager.LoadPolicies([stale]);

        Assert.Empty(manager.Quarantined);
        Assert.Empty(Assert.Single(manager.Dropped).Excused);
    }

    // The other half of the same rule: with nothing dropped, a field no member declares is
    // the typo it always was and still takes the row.
    [Fact]
    public void A_constraint_no_member_declares_still_quarantines_when_no_key_was_dropped()
    {
        var manager = Recording();

        var typo = new Policy
        {
            Id = Guid.NewGuid(),
            Name = "typo",
            Messages = [Key, UpdateKey],
            Audience = new() { Authenticated = true },
            Fields = new Dictionary<string, PolicyConstraint> { ["PatientId"] = PolicyConstraint.Me },
        };

        manager.LoadPolicies([typo]);

        Assert.Same(typo, Assert.Single(manager.Quarantined));
        Assert.Empty(manager.Dropped);
    }

    // The drop is the stored row's, never a built-in's: those are code, so a key the registry
    // does not know is a deploy-time bug and has to stay loud (permissions.md).
    [Fact]
    public void A_built_in_naming_a_message_the_registry_does_not_know_still_fails_hard()
    {
        var builtIn = new Policy { Name = "built-in", Messages = [Gone, Key], Audience = new() { Authenticated = true } };

        var exception = Assert.Throws<InvalidOperationException>(() => Manager(null, builtIn));

        Assert.Contains(Gone, exception.Message);
    }

    // What the org filter reads behind the gate (OrgScopeInterceptor): the verdict says whether
    // what allowed THIS send was authored across the organizations, so a caller holding both a
    // counter's grant and their own gets the wider answer only for the send their own allowed.
    [Fact]
    public async Task The_verdict_says_when_the_grant_that_allowed_the_send_crosses_the_organizations()
    {
        var own = new Policy
        {
            Name = "their own record",
            Messages = [Key],
            Audience = new() { HasRole = "medico" },
            Fields = new Dictionary<string, PolicyConstraint> { ["PartyId"] = PolicyConstraint.Me },
            EveryOrganization = true,
        };

        var counter = new Policy
        {
            Name = "the counter's list",
            Messages = [Key],
            Audience = new() { HasRole = "medico" },
        };

        var manager = Manager(null, own, counter);

        Assert.True((await manager.Authorize(new TestMessage { PartyId = Me }, Doctor())).EveryOrganization);
        Assert.False((await manager.Authorize(new TestMessage { PartyId = Other }, Doctor())).EveryOrganization);
    }

    [Fact]
    public async Task A_grant_that_says_nothing_about_it_keeps_the_callers_own_organization()
    {
        var manager = Manager(null, new Policy { Name = "t", Messages = [Key], Audience = new() { HasRole = "medico" } });

        Assert.False((await manager.Authorize(new TestMessage(), Doctor())).EveryOrganization);
    }

    // The pattern expands and nothing it covers declares PartyId, which is a document fault
    // rather than a stale key: it takes the row, which is what keeps the quarantine tests above
    // honest. The wildcard itself is no longer the fault (nsail#1702) — the unbound field is.
    static Policy Broken()
    {
        return new Policy
        {
            Id = Guid.NewGuid(),
            Name = "broken",
            Messages = ["Security.*"],
            Audience = new() { Authenticated = true },
            Fields = new Dictionary<string, PolicyConstraint> { ["PartyId"] = PolicyConstraint.Me },
        };
    }
}
