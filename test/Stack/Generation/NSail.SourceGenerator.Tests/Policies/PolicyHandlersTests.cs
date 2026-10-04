// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NSail.Messaging;
using NSail.Messaging.Runtime;
using NSail.Security;
using NSail.SourceGeneration.Annotations;
using NSail.SourceGeneration.Testing;
using NSail.SourceGenerator.Generation.Policies;
using System.Linq;

namespace NSail.SourceGenerator.Tests.Policies;

public class PolicyHandlersTests
{
    [Fact]
    public void PolicyHandlers_Generated_From_Marked_Fields()
    {
        var builder = new CompilationUnitBuilder()
            .AddSource("Messages.cs", """
                using Microsoft.Extensions.DependencyInjection;
                using NSail.Messaging;
                using NSail.Security.Annotations;
                using NSail.SourceGeneration.Annotations;

                namespace Acme.Clinic.Prescriptions;

                public class CreatePrescription : IMessage
                {
                    [PolicyField(RestrictAs.Party)]
                    public System.Guid? PatientId { get; set; }

                    [PolicyField(RestrictAs.Organization)]
                    public System.Guid? OrganizationId { get; set; }

                    [PolicyField(RestrictAs.Reference)]
                    public System.Guid TreatmentId { get; set; }

                    [PolicyField(RestrictAs.Party)]
                    public System.Collections.Generic.List<System.Guid> AttendeePartyIds { get; set; } = new();

                    [PolicyField(RestrictAs.Flag)]
                    public bool OutsideProtocol { get; set; }

                    public string? Notes { get; set; }
                }

                public class Unmarked : IMessage
                {
                    public System.Guid? PartyId { get; set; }
                }

                public static partial class PolicyHandlers
                {
                    [Generated(Policies.Handlers)]
                    public static partial void AddClinicPolicyHandlers(this IServiceCollection services);
                }
                """)
            .AddFrameworkReferences("net10.0")
            .AddContainingAssembly<Microsoft.Extensions.DependencyInjection.IServiceCollection>()
            .AddContainingAssembly<GeneratedAttribute>()
            .AddContainingAssembly<IMessage>()
            .AddContainingAssembly<Policy>()
            .AddContainingAssembly<MessageRegistration>()
            .AddContainingAssembly<NSail.Security.Annotations.PolicyFieldAttribute>();

        var compilation = builder.Build("PolicyHandlersTestAssembly");

        var generator = new PolicyHandlersSourceGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
        Assert.Empty(outputCompilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));

        var generated = Assert.Single(driver.GetRunResult().GeneratedTrees).ToString();

        Assert.Contains("CreatePrescriptionPolicyHandler", generated);
        Assert.Contains("Satisfies(\"PatientId\", m.PatientId, global::NSail.Security.Annotations.RestrictAs.Party", generated);
        Assert.Contains("Satisfies(\"OrganizationId\", m.OrganizationId, global::NSail.Security.Annotations.RestrictAs.Organization", generated);
        // The axis travels beside the name: the editor offers "@me" only where the field is a
        // Party, and it reads that from the same registry the evaluator checks against.
        Assert.Contains("[\"PatientId\"] = global::NSail.Security.Annotations.RestrictAs.Party", generated);
        Assert.Contains("[\"OrganizationId\"] = global::NSail.Security.Annotations.RestrictAs.Organization", generated);

        // The third axis, and the shape that is not a scalar. Both ride the same emitted call:
        // a collection binds the IEnumerable overload of Satisfies by overload resolution, so
        // the assertion that matters most is the compile above — a collection field with no
        // overload to bind would be an error, not a silently skipped check.
        Assert.Contains("Satisfies(\"TreatmentId\", m.TreatmentId, global::NSail.Security.Annotations.RestrictAs.Reference", generated);
        Assert.Contains("[\"TreatmentId\"] = global::NSail.Security.Annotations.RestrictAs.Reference", generated);
        Assert.Contains("Satisfies(\"AttendeePartyIds\", m.AttendeePartyIds, global::NSail.Security.Annotations.RestrictAs.Party", generated);
        Assert.Contains("[\"AttendeePartyIds\"] = global::NSail.Security.Annotations.RestrictAs.Party", generated);

        // The fourth axis emits the one call that names no axis and asks nothing of the
        // session: a flag is answered from the policy's own literal, so there is no symbol to
        // weigh and no graph to reach. The pairing is what makes a mismatch a compile error
        // rather than a silent denial — a Guid marked Flag binds no overload here, and a bool
        // marked Party binds none in the branch above.
        Assert.Contains("if (!Satisfies(\"OutsideProtocol\", m.OutsideProtocol))", generated);
        Assert.Contains("[\"OutsideProtocol\"] = global::NSail.Security.Annotations.RestrictAs.Flag", generated);

        // Every message is in the registry, marked or not: a policy expands into one handler
        // per message it lists, so one that is missing cannot be granted at all. The
        // unmarked message simply has nothing to check.
        Assert.Contains("UnmarkedPolicyHandler", generated);
        Assert.Contains("RestrictAs>(global::System.StringComparer.Ordinal) {  }", generated);
        Assert.DoesNotContain("Satisfies(\"PartyId\"", generated);

        // The message registry is emitted from this same walk, one entry per message beside
        // its factory — which is what lets the WebAssembly client, where no handler is ever
        // registered, hold the identical set the server does (nsail#348).
        Assert.Contains("new global::NSail.Messaging.Runtime.MessageRegistration(typeof(global::Acme.Clinic.Prescriptions.CreatePrescription))", generated);
        Assert.Contains("new global::NSail.Messaging.Runtime.MessageRegistration(typeof(global::Acme.Clinic.Prescriptions.Unmarked))", generated);
    }

    // The implication rides the same registration the fields do: the evaluator builds its
    // dependency handlers from what the contract declared, so nobody names the lookup in a
    // policy — and the client, where the trimmer has been through the attributes, reads the
    // same list the server does.
    [Fact]
    public void The_requires_declarations_are_emitted_beside_the_fields()
    {
        var builder = new CompilationUnitBuilder()
            .AddSource("Messages.cs", """
                using Microsoft.Extensions.DependencyInjection;
                using NSail.Messaging;
                using NSail.Security.Annotations;
                using NSail.SourceGeneration.Annotations;

                namespace Acme.Clinic.Prescriptions;

                public class LookupPatients : IMessage
                {
                }

                public class LookupTreatments : IMessage
                {
                }

                [Requires(typeof(LookupPatients))]
                [Requires(typeof(LookupTreatments))]
                public class CreatePrescription : IMessage
                {
                    [PolicyField(RestrictAs.Party)]
                    public System.Guid? PatientId { get; set; }
                }

                public static partial class PolicyHandlers
                {
                    [Generated(Policies.Handlers)]
                    public static partial void AddClinicPolicyHandlers(this IServiceCollection services);
                }
                """)
            .AddFrameworkReferences("net10.0")
            .AddContainingAssembly<Microsoft.Extensions.DependencyInjection.IServiceCollection>()
            .AddContainingAssembly<GeneratedAttribute>()
            .AddContainingAssembly<IMessage>()
            .AddContainingAssembly<Policy>()
            .AddContainingAssembly<MessageRegistration>()
            .AddContainingAssembly<NSail.Security.Annotations.PolicyFieldAttribute>();

        var compilation = builder.Build("PolicyRequiresTestAssembly");

        GeneratorDriver driver = CSharpGeneratorDriver.Create(new PolicyHandlersSourceGenerator());

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
        Assert.Empty(outputCompilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));

        var generated = Assert.Single(driver.GetRunResult().GeneratedTrees).ToString();

        Assert.Contains("Requires = new global::System.Type[] { typeof(global::Acme.Clinic.Prescriptions.LookupPatients), typeof(global::Acme.Clinic.Prescriptions.LookupTreatments) }", generated);

        // A message that declares nothing carries an empty list, not an absent property: the
        // evaluator reads every factory the same way.
        Assert.Contains("(typeof(global::Acme.Clinic.Prescriptions.LookupPatients), static policy => new LookupPatientsPolicyHandler(policy)) { Fields = new global::System.Collections.Generic.Dictionary<string, global::NSail.Security.Annotations.RestrictAs>(global::System.StringComparer.Ordinal) {  }, Requires = new global::System.Type[] {  } }", generated);
    }
}
