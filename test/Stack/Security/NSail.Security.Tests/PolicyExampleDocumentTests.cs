// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Text.Json;
using NSail.Security;

namespace NSail.Security.Tests;

// testing.md ruling 4: a documented wire format obliges a test per documented shape. These are
// permissions.md's own Examples, copied character for character — the doctor row, the admin
// row, the root row, and the built-in the pre-auth section shows. A reader who pastes one into
// the Policies table is entitled to a row that loads; doctrine that cannot be executed is
// prose fiction, and a policy that fails to load is a grant that silently does nothing.
public sealed class PolicyExampleDocumentTests
{
    [Fact]
    public void The_doctor_row_parses_as_the_prose_beneath_it_says()
    {
        const string document =
            """
            {
              "name": "Doctors create prescriptions for their own patients",
              "messages": ["Optical.Prescriptions.CreatePrescription"],
              "audience": { "memberOf": { "organization": "@current", "role": "medico" } },
              "fields": {
                "PatientId": { "relatedAs": "medico" },
                "DoctorId": "@me"
              }
            }
            """;

        var policy = PolicyDocument.Read(document);

        Assert.Equal(["Optical.Prescriptions.CreatePrescription"], policy.Messages);

        // "@current" is the parametric audience: null here is the symbol, not an absence.
        Assert.Null(policy.Audience?.MemberOf?.Organization);
        Assert.Equal("medico", policy.Audience?.MemberOf?.Role);

        Assert.Equal(PolicyConstraintKind.RelatedAs, policy.Fields!["PatientId"].Kind);
        Assert.Equal("medico", policy.Fields["PatientId"].Role);
        Assert.Equal(PolicyConstraintKind.Me, policy.Fields["DoctorId"].Kind);
    }

    [Fact]
    public void The_admin_row_parses_with_no_fields_at_all()
    {
        const string document =
            """
            {
              "name": "Admins create any prescription",
              "messages": ["Optical.Prescriptions.CreatePrescription"],
              "audience": { "memberOf": { "organization": "@current", "role": "admin" } }
            }
            """;

        var policy = PolicyDocument.Read(document);

        // No fields = full capability. An empty dictionary would read the same to a careless
        // eye and mean the same thing to the evaluator, but the doc's sentence is "absent".
        Assert.Null(policy.Fields);
        Assert.Equal("admin", policy.Audience?.MemberOf?.Role);
    }

    [Fact]
    public void The_root_row_parses_as_a_wildcard_over_everything()
    {
        const string document =
            """
            {
              "name": "System Administrator",
              "messages": ["*"],
              "audience": { "hasRole": "admin" }
            }
            """;

        var policy = PolicyDocument.Read(document);

        Assert.Equal(["*"], policy.Messages);

        // hasRole, deliberately not memberOf @current: a system admin transcends the active
        // organization, which is the one spot where the distinction has big consequences.
        Assert.Equal("admin", policy.Audience?.HasRole);
        Assert.Null(policy.Audience?.MemberOf);
    }

    [Fact]
    public void The_pre_auth_row_parses_as_anonymous()
    {
        const string document =
            """
            { "name": "Anyone can sign in", "messages": ["Iam.Authentication.SignIn"] }
            """;

        var policy = PolicyDocument.Read(document);

        // No audience = anonymous. Reading a missing audience as an empty one would turn an
        // anonymous grant into an authenticated-only one, or the reverse.
        Assert.Null(policy.Audience);
    }

    // Being built-in is the SOURCE a policy arrives from, never a key it carries: the evaluator
    // aggregates code-injected rows separately from stored ones, and a stored row claiming it
    // would be a document asserting its own indestructibility. The page showed the key; the
    // parser now says what it always meant.
    [Fact]
    public void A_document_claiming_to_be_built_in_is_refused()
    {
        const string document =
            """
            { "name": "Anyone can sign in", "messages": ["Iam.Authentication.SignIn"], "builtIn": true }
            """;

        Assert.ThrowsAny<JsonException>(() => PolicyDocument.Read(document));
    }

    // The reason the refusal above is worth having at all. A key the reader cannot see is
    // wrong — one letter out of "fields" — used to leave a policy with no constraints, which
    // is not a narrower grant but the widest one there is.
    [Fact]
    public void A_mistyped_key_never_degrades_into_a_full_capability_policy()
    {
        const string document =
            """
            { "name": "t", "messages": ["x"], "feilds": { "DoctorId": "@me" } }
            """;

        Assert.ThrowsAny<JsonException>(() => PolicyDocument.Read(document));
    }
}
