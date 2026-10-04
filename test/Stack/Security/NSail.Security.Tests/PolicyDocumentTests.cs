// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Text.Json;
using NSail.Security;

namespace NSail.Security.Tests;

public sealed class PolicyDocumentTests
{
    [Fact]
    public void A_document_survives_the_round_trip_whole()
    {
        var pinned = Guid.NewGuid();
        var party = Guid.NewGuid();

        var original = PolicyBuilder
            .Named("Doctors manage their own patients' prescriptions")
            .For("Optical.Prescriptions.CreatePrescription", "Optical.Prescriptions.ListPrescriptions")
            .ForParty(party)
            .Between(new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31))
            .Restrict("PatientId", PolicyConstraint.RelatedAs("medico"))
            .Restrict("OrganizationId", PolicyConstraint.Literal(pinned))
            .Build();

        var read = PolicyDocument.Read(PolicyDocument.Write(original));

        Assert.Equal(original.Name, read.Name);
        Assert.Equal(original.Messages, read.Messages);
        Assert.Equal(party, read.Audience?.Is);
        Assert.Equal(original.ValidFrom, read.ValidFrom);
        Assert.Equal(original.ValidTo, read.ValidTo);

        Assert.Equal(PolicyConstraintKind.RelatedAs, read.Fields!["PatientId"].Kind);
        Assert.Equal("medico", read.Fields["PatientId"].Role);
        Assert.Equal([pinned], read.Fields["OrganizationId"].Values);
    }

    [Fact]
    public void An_audienceless_policy_stays_audienceless()
    {
        var read = PolicyDocument.Read(PolicyDocument.Write(
            PolicyBuilder.Named("Anyone can sign in").For("Iam.Authentication.SignIn").ForAnyone().Build()));

        // Reading a missing audience as an empty one would turn an anonymous grant into an
        // authenticated-only one, or the reverse. It has to stay absent.
        Assert.Null(read.Audience);
    }

    [Fact]
    public void A_row_already_in_a_database_still_reads()
    {
        // Copied out of the Policies table after the seed migration ran. A change to the
        // serialization contract that breaks stored rows fails here instead of at a
        // customer's next startup, where the symptom is a grant that silently does nothing.
        const string stored =
            """{"name":"Authenticated users can change their own password","messages":["Iam.Users.ChangePassword"],"audience":{"authenticated":true}}""";

        var policy = PolicyDocument.Read(stored);

        Assert.Equal("Authenticated users can change their own password", policy.Name);
        Assert.Equal(["Iam.Users.ChangePassword"], policy.Messages);
        Assert.True(policy.Audience?.Authenticated);
        Assert.Null(policy.Fields);
    }

    [Fact]
    public void A_current_organization_membership_round_trips_as_the_symbol()
    {
        var document = PolicyDocument.Write(PolicyBuilder
            .Named("t")
            .For("x")
            .ForMembers(organizationId: null, role: "medico")
            .Build());

        // The symbol travels as a bare string, exactly like the pseudocode in permissions.md
        // — a stored row should read "@current" rather than an omitted key.
        Assert.Contains("\"@current\"", document);

        var read = PolicyDocument.Read(document);

        Assert.Null(read.Audience?.MemberOf?.Organization);
        Assert.Equal("medico", read.Audience?.MemberOf?.Role);
    }

    [Fact]
    public void A_literal_organization_membership_round_trips_as_the_id()
    {
        var organization = Guid.NewGuid();

        var read = PolicyDocument.Read(PolicyDocument.Write(PolicyBuilder
            .Named("t")
            .For("x")
            .ForMembers(organization)
            .Build()));

        Assert.Equal(organization, read.Audience?.MemberOf?.Organization);
    }

    [Fact]
    public void The_constraint_vocabulary_is_stored_by_name()
    {
        var document = PolicyDocument.Write(PolicyBuilder
            .Named("t")
            .For("x")
            .Restrict("PartyId", PolicyConstraint.Me)
            .Build());

        // A number here would renumber itself the day the enum grows a member in the middle.
        Assert.Contains("\"Me\"", document);
    }

    [Theory]
    [InlineData("\"@me\"", PolicyConstraintKind.Me)]
    [InlineData("\"@current\"", PolicyConstraintKind.Current)]
    [InlineData("\"@memberOf\"", PolicyConstraintKind.MemberOf)]
    [InlineData("\"@memberOfOrDescendant\"", PolicyConstraintKind.MemberOfOrDescendant)]
    [InlineData("\"*\"", PolicyConstraintKind.Any)]
    public void A_symbol_string_reads_as_the_constraint_it_names(string constraint, PolicyConstraintKind expected)
    {
        var read = PolicyDocument.Read(Constraining(constraint));

        Assert.Equal(expected, read.Fields!["DoctorId"].Kind);
    }

    [Fact]
    public void A_parameterized_object_reads_as_the_relation_it_names()
    {
        var read = PolicyDocument.Read(Constraining("""{"relatedAs":"medico"}"""));

        Assert.Equal(PolicyConstraintKind.RelatedAs, read.Fields!["DoctorId"].Kind);
        Assert.Equal("medico", read.Fields["DoctorId"].Role);
    }

    [Fact]
    public void A_bare_id_reads_as_a_literal()
    {
        var pinned = Guid.NewGuid();

        var read = PolicyDocument.Read(Constraining($"\"{pinned}\""));

        Assert.Equal(PolicyConstraintKind.Literal, read.Fields!["DoctorId"].Kind);
        Assert.Equal([pinned], read.Fields["DoctorId"].Values);
    }

    [Fact]
    public void An_array_of_literals_reads_as_the_whole_set()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        // One policy pinning several organizations is the reason the array shape exists —
        // reading only the first would silently narrow the grant to one of them.
        var read = PolicyDocument.Read(Constraining($"""["{first}","{second}"]"""));

        Assert.Equal(PolicyConstraintKind.Literal, read.Fields!["DoctorId"].Kind);
        Assert.Equal([first, second], read.Fields["DoctorId"].Values);
    }

    // The fourth literal shape, and the only one that is not an id: a flag field is pinned to
    // true or false, which is what gates who may tick an override (permissions.md,
    // RestrictAs.Flag).
    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    public void A_bare_boolean_reads_as_the_flag_the_policy_pins(string constraint, bool expected)
    {
        var read = PolicyDocument.Read(Constraining(constraint));

        Assert.Equal(PolicyConstraintKind.Literal, read.Fields!["DoctorId"].Kind);
        Assert.Equal(expected, read.Fields["DoctorId"].Flag);
        Assert.Empty(read.Fields["DoctorId"].Values);
    }

    [Fact]
    public void A_pinned_flag_comes_back_canonical_and_stays_there()
    {
        var written = PolicyDocument.Write(PolicyDocument.Read(Constraining("false")));

        // No values array beside it: the two are alternative payloads of one kind, and an
        // empty list is the shape the reader refuses everywhere else.
        Assert.Contains("\"DoctorId\":{\"kind\":\"Literal\",\"flag\":false}", written, StringComparison.Ordinal);
        Assert.Equal(written, PolicyDocument.Write(PolicyDocument.Read(written)));
    }

    [Fact]
    public void The_expanded_form_a_stored_row_carries_still_reads()
    {
        var pinned = Guid.NewGuid();

        var relation = PolicyDocument.Read(Constraining("""{"kind":"RelatedAs","role":"medico","values":[]}"""));
        var literal = PolicyDocument.Read(Constraining("{\"kind\":\"Literal\",\"values\":[\"" + pinned + "\"]}"));

        Assert.Equal(PolicyConstraintKind.RelatedAs, relation.Fields!["DoctorId"].Kind);
        Assert.Equal("medico", relation.Fields["DoctorId"].Role);
        Assert.Equal([pinned], literal.Fields!["DoctorId"].Values);
    }

    [Fact]
    public void A_compact_document_comes_back_canonical_and_stays_there()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        var compact =
            """{"name":"t","messages":["x"],"fields":{"DoctorId":"@me","PatientId":{"relatedAs":"medico"},"OrganizationId":["""
            + "\"" + first + "\",\"" + second + "\"]}}";

        var written = PolicyDocument.Write(PolicyDocument.Read(compact));

        Assert.Contains("\"DoctorId\":{\"kind\":\"Me\",\"values\":[]}", written, StringComparison.Ordinal);
        Assert.Contains("\"PatientId\":{\"kind\":\"RelatedAs\",\"role\":\"medico\",\"values\":[]}", written, StringComparison.Ordinal);
        Assert.Contains($"\"OrganizationId\":{{\"kind\":\"Literal\",\"values\":[\"{first}\",\"{second}\"]}}", written, StringComparison.Ordinal);

        // Compact in, canonical out — and canonical in, the same canonical out, or every
        // load would rewrite the row into a third shape.
        Assert.Equal(written, PolicyDocument.Write(PolicyDocument.Read(written)));
    }

    [Theory]
    [InlineData("\"@nobody\"")]
    [InlineData("\"@Me\"")]
    [InlineData("\"medico\"")]
    [InlineData("null")]
    [InlineData("7")]
    [InlineData("[]")]
    [InlineData("[\"@me\"]")]
    [InlineData("{}")]
    [InlineData("{\"role\":\"medico\"}")]
    [InlineData("{\"relatedAs\":\"\"}")]
    [InlineData("{\"kind\":\"Literal\"}")]
    [InlineData("{\"kind\":\"Literal\",\"values\":[]}")]
    [InlineData("{\"kind\":\"Nonsense\"}")]
    [InlineData("{\"kind\":\"Me\",\"role\":\"medico\"}")]
    [InlineData("{\"kind\":\"Me\",\"values\":[\"7f2c1e94-0000-4000-8000-000000000001\"]}")]
    [InlineData("{\"kind\":\"RelatedAs\",\"role\":\"medico\",\"values\":[\"7f2c1e94-0000-4000-8000-000000000001\"]}")]
    [InlineData("{\"kind\":\"Me\",\"hint\":\"whatever\"}")]
    [InlineData("{\"kind\":\"Literal\",\"flag\":true,\"values\":[\"7f2c1e94-0000-4000-8000-000000000001\"]}")]
    [InlineData("{\"kind\":\"Me\",\"flag\":true}")]
    [InlineData("{\"kind\":\"RelatedAs\",\"role\":\"medico\",\"flag\":true}")]
    [InlineData("{\"kind\":\"Literal\",\"flag\":\"true\"}")]
    public void A_constraint_outside_the_vocabulary_fails_loudly(string constraint)
    {
        // Quarantine is the safe answer only because it is loud. A shape that parsed into
        // nothing would leave a policy that grants and denies without saying which.
        Assert.ThrowsAny<JsonException>(() => PolicyDocument.Read(Constraining(constraint)));
    }

    [Fact]
    public void A_malformed_constraint_never_degrades_into_a_flat_policy()
    {
        // Dropping an unreadable fields block would not merely lose the constraint, it would
        // widen the row into the full-capability shape — the one failure worse than denying.
        Assert.ThrowsAny<JsonException>(() => PolicyDocument.Read(Constraining("""{"kind":"Literal","values":[]}""")));
    }

    [Fact]
    public void A_grant_authored_across_the_organizations_says_so_after_the_round_trip()
    {
        var read = PolicyDocument.Read(PolicyDocument.Write(PolicyBuilder
            .Named("A customer reads their own record")
            .For("Optical.WorkOrders.ListWorkOrders")
            .ForRole("Cliente")
            .Restrict("PatientId", PolicyConstraint.Me)
            .AcrossEveryOrganization()
            .Build()));

        Assert.True(read.EveryOrganization);
    }

    [Fact]
    public void A_document_missing_a_property_this_schema_later_added_reserializes_with_its_default()
    {
        // No "everyOrganization" key at all — the shape a row planted before Policy carried the
        // property still has, forever, in the database it was written to. Reserialize has to
        // read that back as the default the type gives it today, not fail or drop it.
        const string plantedBeforeTheProperty = """{"name":"t","messages":["x"],"fields":{"PartyId":"@me"}}""";

        var canonical = PolicyDocument.Write(PolicyBuilder
            .Named("t")
            .For("x")
            .Restrict("PartyId", PolicyConstraint.Me)
            .Build());

        Assert.Equal(canonical, PolicyDocument.Reserialize(plantedBeforeTheProperty));
    }

    [Fact]
    public void A_document_that_does_not_say_it_keeps_the_callers_own_organization()
    {
        // The row every install already holds: the key is absent, and absent has to read as the
        // narrow answer or a deploy would widen every stored grant at once.
        var read = PolicyDocument.Read("""{"name":"t","messages":["x"]}""");

        Assert.False(read.EveryOrganization);
    }

    // What a seed step that has to reach a row it already planted is entitled to change: the one
    // field nobody answered about, and nothing else.
    [Fact]
    public void A_merged_constraint_lands_on_the_field_nobody_answered_about()
    {
        var merged = PolicyDocument.WithConstraint(
            """{"name":"t","messages":["x"],"fields":{"OwnerPartyId":"@me"}}""",
            "OrganizationId",
            PolicyConstraint.MemberOfOrDescendant);

        var policy = PolicyDocument.Read(merged);

        Assert.Equal("t", policy.Name);
        Assert.Equal(PolicyConstraintKind.Me, policy.Fields!["OwnerPartyId"].Kind);
        Assert.Equal(PolicyConstraintKind.MemberOfOrDescendant, policy.Fields["OrganizationId"].Kind);
    }

    [Fact]
    public void A_merged_constraint_leaves_an_answered_field_alone()
    {
        const string document = """{"name":"t","messages":["x"],"fields":{"OrganizationId":"@current"}}""";

        Assert.Same(
            document,
            PolicyDocument.WithConstraint(document, "OrganizationId", PolicyConstraint.MemberOfOrDescendant));
    }

    // A flat row is the other shape a retrofit meets, and the one where a merge that assumed a
    // dictionary would throw instead of planting.
    [Fact]
    public void A_merged_constraint_reaches_a_row_with_no_fields_block_at_all()
    {
        var merged = PolicyDocument.WithConstraint(
            """{"name":"t","messages":["x"]}""",
            "OrganizationId",
            PolicyConstraint.MemberOf);

        Assert.Equal(
            PolicyConstraintKind.MemberOf,
            PolicyDocument.Read(merged).Fields!["OrganizationId"].Kind);
    }

    // What nsail#1797's tenant step is entitled to change: the one message a shop's import never
    // carried, and nothing else.
    [Fact]
    public void A_merged_message_lands_on_a_row_missing_it()
    {
        var merged = PolicyDocument.WithMessage(
            """{"name":"t","messages":["x"],"fields":{"OwnerPartyId":"@me"}}""",
            "y");

        var policy = PolicyDocument.Read(merged);

        Assert.Equal("t", policy.Name);
        Assert.Equal(["x", "y"], policy.Messages);
        Assert.Equal(PolicyConstraintKind.Me, policy.Fields!["OwnerPartyId"].Kind);
    }

    [Fact]
    public void A_merged_message_leaves_a_row_that_already_carries_it_alone()
    {
        const string document = """{"name":"t","messages":["x","y"]}""";

        Assert.Same(document, PolicyDocument.WithMessage(document, "y"));
    }

    // Both merges rewrite the whole row, so a property neither one is told to change has to
    // survive the round trip untouched — EveryOrganization included, the flag nsail#1797 added
    // back to WithConstraint's own Write and gave to WithMessage from the start.
    [Fact]
    public void A_merged_constraint_keeps_the_rows_own_EveryOrganization()
    {
        var document = PolicyDocument.Write(PolicyBuilder
            .Named("t")
            .For("x")
            .Restrict("OwnerPartyId", PolicyConstraint.Me)
            .AcrossEveryOrganization()
            .Build());

        var merged = PolicyDocument.WithConstraint(document, "OrganizationId", PolicyConstraint.MemberOfOrDescendant);

        Assert.True(PolicyDocument.Read(merged).EveryOrganization);
    }

    [Fact]
    public void A_merged_message_keeps_the_rows_own_EveryOrganization()
    {
        var document = PolicyDocument.Write(PolicyBuilder
            .Named("t")
            .For("x")
            .AcrossEveryOrganization()
            .Build());

        var merged = PolicyDocument.WithMessage(document, "y");

        Assert.True(PolicyDocument.Read(merged).EveryOrganization);
    }

    static string Constraining(string constraint)
    {
        return """{"name":"t","messages":["x"],"fields":{"DoctorId":""" + constraint + "}}";
    }
}
