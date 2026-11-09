// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NSail.Mapping;
using NSail.Mapping.Annotations;
using NSail.SourceGeneration.Annotations;
using NSail.SourceGeneration.Testing;
using NSail.SourceGenerator.Generation.Mappers;
using System.Linq;

namespace NSail.SourceGenerator.Tests.Mappers;

/// <summary>What the store's model declares makes a keyed type of this assembly a row, whether
/// a context's DbSet or a ModelBuilder.Entity call declares it.</summary>
public class MapperModelTests
{
    const string Types = """
        public class Customer { public System.Guid Id { get; set; } public string Name { get; set; } = ""; }

        public class UpdateOrder { public System.Guid Id { get; set; } public Customer Customer { get; set; } = null!; }

        [Entity, MapFrom(typeof(UpdateOrder))]
        public class Order { public System.Guid Id { get; set; } public Customer Customer { get; set; } = null!; }
        """;

    [Fact]
    public void A_DbSet_of_a_context_makes_its_keyed_type_a_row()
    {
        var bare = Generate(string.Empty);
        var modelled = Generate("public class ShopContext : DbContext { public DbSet<Customer> Customers { get; set; } = null!; }");

        Assert.NotEqual(bare, modelled);
    }

    [Fact]
    public void An_Entity_call_models_its_entity_as_a_DbSet_does()
    {
        var asSet = Generate("public class ShopContext : DbContext { public DbSet<Customer> Customers { get; set; } = null!; }");
        var asCall = Generate("""
            public class ShopContext : DbContext
            {
                protected override void OnModelCreating(ModelBuilder modelBuilder)
                {
                    modelBuilder.Entity<Customer>();
                }
            }
            """);

        Assert.Equal(asSet, asCall);
    }

    static string Generate(string context)
    {
        var compilation = new CompilationUnitBuilder()
            .AddSource("Types.cs", $$"""
                using Microsoft.EntityFrameworkCore;
                using Microsoft.Extensions.DependencyInjection;
                using NSail.Mapping.Annotations;
                using NSail.SourceGeneration.Annotations;

                namespace Acme.Shop;

                {{Types}}

                {{context}}

                public static partial class Mappings
                {
                    [Generated(Mappers.Entities)]
                    public static partial void AddShopMappings(this IServiceCollection services);
                }
                """)
            .AddFrameworkReferences("net10.0")
            .AddContainingAssembly<Microsoft.Extensions.DependencyInjection.IServiceCollection>()
            .AddContainingAssembly<Microsoft.EntityFrameworkCore.DbContext>()
            .AddContainingAssembly<GeneratedAttribute>()
            .AddContainingAssembly<MapFromAttribute>()
            .AddContainingAssembly<MapContext>()
            .AddContainingAssembly<NSail.Messaging.Annotations.AsRouteAttribute>()
            .Build("MapperModelAssembly");

        GeneratorDriver driver = CSharpGeneratorDriver.Create(new MappersSourceGenerator());

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out _);

        Assert.Empty(output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));

        return string.Join("\n", driver.GetRunResult().GeneratedTrees.Select(t => t.ToString()));
    }
}
