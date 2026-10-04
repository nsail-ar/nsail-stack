// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using NSail.Security;
using NSail.Security.Annotations;

namespace NSail.Security.Tests;

public sealed class PolicyBuilderTests
{
    [Fact]
    public void It_builds_the_self_service_policy_the_seed_needs()
    {
        var policy = PolicyBuilder
            .Named("Authenticated users can change their own password")
            .For("Iam.Users.ChangePassword")
            .ForAnySession()
            .Build();

        Assert.Equal(["Iam.Users.ChangePassword"], policy.Messages);
        Assert.True(policy.Audience?.Authenticated);
        Assert.Null(policy.Fields);
    }

    [Fact]
    public void A_verb_family_is_one_document()
    {
        var policy = PolicyBuilder
            .Named("Doctors manage their own patients' prescriptions")
            .For("Optical.Prescriptions.CreatePrescription", "Optical.Prescriptions.ListPrescriptions")
            .ForRole("medico")
            .Restrict("PatientId", PolicyConstraint.RelatedAs("medico"))
            .Build();

        Assert.Equal(2, policy.Messages.Count);
        Assert.Equal("medico", policy.Audience?.HasRole);
        Assert.Equal(PolicyConstraintKind.RelatedAs, policy.Fields!["PatientId"].Kind);
    }

    [Fact]
    public void An_audience_is_absent_only_when_it_is_asked_for()
    {
        var anonymous = PolicyBuilder.Named("Anyone can sign in").For("Iam.Authentication.SignIn").ForAnyone().Build();

        Assert.Null(anonymous.Audience);
    }

    [Fact]
    public void The_last_audience_wins_rather_than_stacking()
    {
        var policy = PolicyBuilder.Named("t").For("x").ForRole("medico").ForAnySession().Build();

        // AND within an audience is the document's business; the builder sets one, so a
        // second call replaces rather than silently combining into something unasked for.
        Assert.Null(policy.Audience?.HasRole);
        Assert.True(policy.Audience?.Authenticated);
    }

    [Fact]
    public void A_repeated_message_is_listed_once()
    {
        var policy = PolicyBuilder.Named("t").For("x", "x").For("x").Build();

        Assert.Equal(["x"], policy.Messages);
    }

    [Fact]
    public void A_policy_without_messages_does_not_build()
    {
        Assert.Throws<InvalidOperationException>(() => PolicyBuilder.Named("t").ForAnySession().Build());
    }

    [Fact]
    public void What_it_builds_survives_validation()
    {
        var policy = PolicyBuilder
            .Named("Authenticated users can change their own password")
            .For("Security.Tests.TestMessage")
            .ForAnySession()
            .Build();

        var factories = new[]
        {
            new PolicyHandlerFactory(typeof(TestMessage), p => new TestMessagePolicyHandler(p))
            {
                Fields = new Dictionary<string, RestrictAs>
                {
                    ["PartyId"] = RestrictAs.Party,
                    ["OrganizationId"] = RestrictAs.Organization,
                },
            },
        };

        var manager = new SecurityManager(Registry.For(factories), new FakeRelations(), factories, []);

        var validation = manager.ValidateDocument(policy);

        Assert.True(validation.IsValid);
        Assert.False(validation.IsAnonymous);
    }
}
