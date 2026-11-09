// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Collections.Immutable;
using System.Linq;
using Microsoft.AspNetCore.Routing;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NSail.Messaging;
using NSail.Messaging.Annotations;
using NSail.Messaging.Runtime;
using NSail.SourceGeneration.Annotations;
using NSail.SourceGeneration.Testing;
using NSail.SourceGenerator.Generation.Http;
using NSail.SourceGenerator.Generation.Policies;
using NSail.SourceGenerator.Generation.Services;

namespace NSail.SourceGenerator.Tests.Shapes;

/// <summary>The declaration shapes a generator meets that are not the usual one: a type with no
/// namespace, a nested one, a holder or a [Source] that cannot be honoured. Each either
/// generates code that compiles or says why it cannot.</summary>
public class DeclarationShapeTests
{
    const string Usings = """
        using Microsoft.Extensions.DependencyInjection;
        using NSail.Messaging;
        using NSail.Messaging.Annotations;
        using NSail.SourceGeneration.Annotations;

        """;

    const string ClientsHolder = """
        namespace Acme.Holders
        {
            public static partial class Clients
            {
                [Generated(global::NSail.SourceGeneration.Annotations.Http.Clients)]
                public static partial void AddAcmeClients(this IServiceCollection services);
            }
        }
        """;

    const string EndpointsHolder = """
        namespace Acme.Holders
        {
            public static partial class Endpoints
            {
                [Generated(global::NSail.SourceGeneration.Annotations.Http.Endpoints)]
                public static partial void AddAcmeEndpoints(this IServiceCollection services);
            }
        }
        """;

    const string GlobalMessage = """
        [Http(Method.Get, "global/ping")]
        public class GlobalPing : IMessage
        {
            public string Name { get; set; } = "";
        }

        [Http(Method.Post, "global/post")]
        public class GlobalPost : IMessage
        {
            public string Name { get; set; } = "";
        }

        """;

    const string NestedMessages = """
        namespace Acme.Billing
        {
            public class Outer
            {
                public class Middle
                {
                    [Http(Method.Post, "nested/deep")]
                    public class Deep : IMessage
                    {
                        public string Name { get; set; } = "";
                    }
                }

                [Http(Method.Post, "nested/one")]
                public class One : IMessage
                {
                    public string Name { get; set; } = "";
                }
            }

            public class Other
            {
                [Http(Method.Post, "nested/other-one")]
                public class One : IMessage
                {
                    public string Name { get; set; } = "";
                }
            }
        }

        """;

    [Fact]
    public void A_global_namespace_message_and_a_nested_one_get_clients_that_compile()
    {
        var run = Run(new HttpClientsSourceGenerator(), GlobalMessage + NestedMessages + ClientsHolder);

        Assert.Empty(run.Errors);
        Assert.Contains(run.Sources, s => s.Contains("class GlobalPingHttpSender"));
        Assert.Contains(run.Sources, s => s.Contains("class Outer_Middle_DeepHttpSender"));
        Assert.Contains(run.Sources, s => s.Contains("class Outer_OneHttpSender"));
        Assert.Contains(run.Sources, s => s.Contains("class Other_OneHttpSender"));
    }

    [Fact]
    public void A_global_namespace_message_and_a_nested_one_get_endpoints_that_compile()
    {
        var run = Run(new HttpEndpointsSourceGenerator(), GlobalMessage + NestedMessages + EndpointsHolder);

        Assert.Empty(run.Errors);
        Assert.Contains(run.Sources, s => s.Contains("class GlobalPostEndpointEntry"));
        Assert.Contains(run.Sources, s => s.Contains("class Outer_Middle_DeepEndpointEntry"));
    }

    [Fact]
    public void Two_holders_covering_one_message_give_distinct_hint_names()
    {
        var second = ClientsHolder.Replace("class Clients", "class MoreClients").Replace("AddAcmeClients", "AddMoreAcmeClients");
        var compilation = Compile(NestedMessages + ClientsHolder + second);

        GeneratorDriver driver = CSharpGeneratorDriver.Create(new HttpClientsSourceGenerator());
        driver = driver.RunGenerators(compilation);

        var result = driver.GetRunResult();

        Assert.Empty(result.Diagnostics);
        Assert.Equal(result.GeneratedTrees.Length, result.GeneratedTrees.Select(t => t.FilePath).Distinct().Count());
    }

    [Fact]
    public void Same_named_messages_in_two_namespaces_keep_one_policy_handler_each()
    {
        var run = Run(new PolicyHandlersSourceGenerator(), """
            namespace Acme.A { public class Close : IMessage { } }
            namespace Acme.B { public class Close : IMessage { } }
            namespace Acme.C { public class Open : IMessage { } }
            namespace Acme.Holders
            {
                public static partial class Policies
                {
                    [Generated(global::NSail.SourceGeneration.Annotations.Policies.Handlers)]
                    public static partial void AddAcmePolicies(this IServiceCollection services);
                }
            }
            """);

        Assert.Empty(run.Errors);

        var source = Assert.Single(run.Sources);

        Assert.Contains("sealed class OpenPolicyHandler", source);
        Assert.Contains("sealed class Acme_A_ClosePolicyHandler", source);
        Assert.Contains("sealed class Acme_B_ClosePolicyHandler", source);
    }

    [Fact]
    public void An_unknown_source_assembly_is_reported()
    {
        var run = Run(new HttpClientsSourceGenerator(), NestedMessages + """
            namespace Acme.Holders
            {
                public static partial class Clients
                {
                    [Source(Assembly = "No.Such.Assembly")]
                    [Generated(global::NSail.SourceGeneration.Annotations.Http.Clients)]
                    public static partial void AddAcmeClients(this IServiceCollection services);
                }
            }
            """);

        Assert.Contains(run.Diagnostics, d => d.Id == "NSG020" && d.GetMessage().Contains("No.Such.Assembly"));
    }

    [Fact]
    public void A_holder_without_a_service_collection_is_reported_and_emits_nothing()
    {
        var run = Run(new ServiceRegistrationSourceGenerator(), """
            namespace Acme.Holders
            {
                public static partial class Services
                {
                    [Generated(global::NSail.SourceGeneration.Annotations.Services.Registration)]
                    public static partial void AddAcmeServices(string name);
                }
            }
            """);

        Assert.Contains(run.Diagnostics, d => d.Id == "NSG022");
        Assert.Empty(run.Sources);
    }

    [Theory]
    [InlineData("public static class Services", "is not a partial class")]
    [InlineData("public static partial class Services<T>", "is generic")]
    public void A_holder_the_generator_cannot_redeclare_is_reported(string declaration, string reason)
    {
        var run = Run(new ServiceRegistrationSourceGenerator(), $$"""
            namespace Acme.Holders
            {
                {{declaration}}
                {
                    [Generated(global::NSail.SourceGeneration.Annotations.Services.Registration)]
                    public static partial void AddAcmeServices(this IServiceCollection services);
                }
            }
            """);

        Assert.Contains(run.Diagnostics, d => d.Id == "NSG021" && d.GetMessage().Contains(reason));
        Assert.Empty(run.Sources);
    }

    [Fact]
    public void A_nested_holder_is_reported()
    {
        var run = Run(new ServiceRegistrationSourceGenerator(), """
            namespace Acme.Holders
            {
                public static partial class Wrapper
                {
                    public static partial class Services
                    {
                        [Generated(global::NSail.SourceGeneration.Annotations.Services.Registration)]
                        public static partial void AddAcmeServices(this IServiceCollection services);
                    }
                }
            }
            """);

        Assert.Contains(run.Diagnostics, d => d.Id == "NSG021" && d.GetMessage().Contains("is nested"));
        Assert.Empty(run.Sources);
    }

    [Fact]
    public void An_http_message_without_a_path_is_reported_and_skipped()
    {
        var run = Run(new HttpClientsSourceGenerator(), """
            namespace Acme.Billing
            {
                [Http(Method.Get, null!)]
                public class NoPath : IMessage { }

                [Http(Method.Get, "fine")]
                public class Fine : IMessage { }
            }

            """ + ClientsHolder);

        Assert.Contains(run.Diagnostics, d => d.Id == "NSG023" && d.GetMessage().Contains("NoPath"));
        Assert.Contains(run.Sources, s => s.Contains("FineHttpSender"));
        Assert.DoesNotContain(run.Sources, s => s.Contains("NoPathHttpSender"));
    }

    [Fact]
    public void The_service_registration_uses_the_holders_own_parameter_name()
    {
        var run = Run(new ServiceRegistrationSourceGenerator(), """
            namespace Acme.Billing
            {
                [Injectable]
                public class Ledger { }
            }
            namespace Acme.Holders
            {
                public static partial class Services
                {
                    [Generated(global::NSail.SourceGeneration.Annotations.Services.Registration)]
                    public static partial void AddAcmeServices(this IServiceCollection collection);
                }
            }
            """, "using NSail.Injection.Annotations;\n");

        Assert.Empty(run.Errors);

        var source = Assert.Single(run.Sources);

        Assert.Contains("collection.AddScoped<Acme.Billing.Ledger, Acme.Billing.Ledger>();", source);
    }

    sealed record Result(ImmutableArray<Diagnostic> Diagnostics, string[] Sources)
    {
        public Diagnostic[] Errors
        {
            get { return Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToArray(); }
        }
    }

    static Result Run(IIncrementalGenerator generator, string code, string extraUsings = "")
    {
        var compilation = Compile(code, extraUsings);

        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var generatorDiagnostics);

        var diagnostics = generatorDiagnostics.AddRange(output.GetDiagnostics());

        return new Result(diagnostics, driver.GetRunResult().GeneratedTrees.Select(t => t.ToString()).ToArray());
    }

    static Compilation Compile(string code, string extraUsings = "")
    {
        return new CompilationUnitBuilder()
            .AddSource("Usings.cs", "global using System.Net.Http;")
            .AddSource("Shapes.cs", Usings + extraUsings + code)
            .AddFrameworkReferences()
            .AddContainingAssembly<Microsoft.Extensions.DependencyInjection.IServiceCollection>()
            .AddContainingAssembly<IEndpointRouteBuilder>()
            .AddContainingAssembly<GeneratedAttribute>()
            .AddContainingAssembly<IMessage>()
            .AddContainingAssembly<HttpAttribute>()
            .AddContainingAssembly<Mediator>()
            .AddContainingAssembly<NSail.Messaging.Http.HttpSender<IMessage>>()
            .AddContainingAssembly<NSail.Security.PolicyHandler>()
            .AddContainingAssembly<NSail.Security.Annotations.PolicyFieldAttribute>()
            .AddContainingAssembly<Microsoft.AspNetCore.Mvc.FromServicesAttribute>()
            .AddContainingAssembly<NSail.Messaging.WebApi.IEndpointEntry>()
            .AddContainingAssembly<NSail.Metadata.MetadataProvider>()
            .AddContainingAssembly<Microsoft.AspNetCore.Routing.RouteData>()
            .AddContainingAssembly<Microsoft.AspNetCore.Http.HttpContext>()
            .AddReference(MetadataReference.CreateFromFile(typeof(Microsoft.AspNetCore.Http.TypedResults).Assembly.Location))
            .AddContainingAssembly<System.Net.Http.HttpClient>()
            .AddContainingAssembly<System.Net.Http.IHttpClientFactory>()
            .AddContainingAssembly<NSail.Injection.Annotations.InjectableAttribute>()
            .Build("ShapesTestAssembly");
    }
}
