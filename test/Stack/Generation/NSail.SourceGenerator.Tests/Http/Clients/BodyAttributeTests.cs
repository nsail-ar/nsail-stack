// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NSail.Messaging;
using NSail.Messaging.Annotations;
using NSail.Security.Annotations;
using NSail.SourceGeneration.Annotations;
using NSail.SourceGeneration.Testing;
using NSail.SourceGenerator.Generation.Http;
using System.Linq;

namespace NSail.SourceGenerator.Tests.Http.Clients;

public class BodyAttributeTests
{
    [Fact]
    public void An_enum_attribute_argument_reaches_the_generated_body_as_the_enum()
    {
        var builder = new CompilationUnitBuilder()
            .AddSource("Messages.cs", """
                using Microsoft.Extensions.DependencyInjection;
                using NSail.Messaging;
                using NSail.Messaging.Annotations;
                using NSail.Security.Annotations;
                using NSail.SourceGeneration.Annotations;

                namespace Acme.Clinic.Prescriptions;

                [Http(Method.Post, "api/prescriptions")]
                public class CreatePrescription : IMessage
                {
                    [PolicyField(RestrictAs.Party)]
                    public System.Guid PatientId { get; set; }

                    [PolicyField(RestrictAs.Organization)]
                    public System.Guid OrganizationId { get; set; }

                    public string? Notes { get; set; }
                }

                public static partial class Clients
                {
                    [Generated(Http.Clients)]
                    public static partial void AddClinicHttpClients(this IServiceCollection services);
                }
                """)
            .AddFrameworkReferences("net10.0")
            .AddContainingAssembly<GeneratedAttribute>()
            .AddContainingAssembly<IMessage>()
            .AddContainingAssembly<HttpAttribute>()
            .AddContainingAssembly<PolicyFieldAttribute>();

        var compilation = builder.Build("ClientBodyAttributeTestAssembly");

        var generator = new HttpClientsSourceGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _);

        var generated = string.Join("\n", driver.GetRunResult().GeneratedTrees.Select(tree => tree.ToString()));

        // The body DTO copies a property's attributes verbatim. An enum constant carries its
        // underlying integer, so writing it raw produced [PolicyField(0)] and the generated
        // file stopped compiling with CS1503 — which is why a message could not carry the
        // marks its own policies need.
        Assert.DoesNotContain("PolicyFieldAttribute(1)", generated);
        Assert.DoesNotContain("PolicyFieldAttribute(2)", generated);

        Assert.Contains("((global::NSail.Security.Annotations.RestrictAs)1)", generated);
        Assert.Contains("((global::NSail.Security.Annotations.RestrictAs)2)", generated);
    }
}
