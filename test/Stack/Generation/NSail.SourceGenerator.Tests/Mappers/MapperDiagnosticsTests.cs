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

/// <summary>What a declaration may not ask for, refused at build time: each of these would
/// otherwise be a mapper that quietly does the wrong thing at run time.</summary>
public class MapperDiagnosticsTests
{
    const string Holder = """
        public static partial class Mappings
        {
            [Generated(Mappers.Entities)]
            public static partial void AddShopMappings(this IServiceCollection services);
        }
        """;

    // A message that happens to carry the branch must not move the row to it by name alone.
    [Fact]
    public void An_organization_axis_mapped_by_name_alone_is_refused()
    {
        var diagnostics = Generate("""
            public class UpdateSale { public System.Guid Id { get; set; } public System.Guid OrganizationId { get; set; } }

            [Entity, MapFrom(typeof(UpdateSale))]
            public class Sale { public System.Guid Id { get; set; } public System.Guid OrganizationId { get; set; } }
            """);

        Assert.Equal("NSG004", Assert.Single(diagnostics).Id);
    }

    // Said explicitly, the move is a decision someone made, and it is mapped.
    [Fact]
    public void An_organization_axis_named_explicitly_is_mapped()
    {
        var diagnostics = Generate("""
            public class UpdateSale { public System.Guid Id { get; set; } public System.Guid OrganizationId { get; set; } }

            [Entity, MapFrom(typeof(UpdateSale))]
            public class Sale
            {
                public System.Guid Id { get; set; }

                [MapFrom(typeof(UpdateSale), "OrganizationId")]
                public System.Guid OrganizationId { get; set; }
            }
            """);

        Assert.Empty(diagnostics);
    }

    // Ported from Detached.Mappers.Tests/Class/Primitive/AsPrimitiveFailTests.cs, where it is an
    // exception at run time.
    [Fact]
    public void map_as_primitive_fails_different_types()
    {
        var diagnostics = Generate("""
            public class InnerDtoClass { public string Name { get; set; } }
            public class InnerEntityClass { public string Name { get; set; } }
            public class RootDto { public InnerDtoClass Mapped { get; set; } public InnerDtoClass Copied { get; set; } }

            [MapFrom(typeof(RootDto))]
            public class RootEntity
            {
                [Composition] public InnerEntityClass Mapped { get; set; }
                [Composition, Primitive] public InnerEntityClass Copied { get; set; }
            }
            """);

        Assert.Equal("NSG005", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public void A_member_with_no_conversion_is_refused()
    {
        var diagnostics = Generate("""
            public class Source { public System.Guid When { get; set; } }

            [MapFrom(typeof(Source))]
            public class Target { public System.DateTime When { get; set; } }
            """);

        Assert.Equal("NSG006", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public void A_target_with_no_parameterless_constructor_is_refused()
    {
        var diagnostics = Generate("""
            public class Source { public int Value { get; set; } }

            [MapFrom(typeof(Source))]
            public class Target
            {
                public Target(int id) { }

                public int Value { get; set; }
            }
            """);

        Assert.Equal("NSG007", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public void A_source_that_is_not_a_class_is_refused()
    {
        var diagnostics = Generate("""
            public interface ISource { int Value { get; } }

            [MapFrom(typeof(ISource))]
            public class Target { public int Value { get; set; } }
            """);

        Assert.Equal("NSG008", Assert.Single(diagnostics).Id);
    }

    // Ported from Detached.Mappers.Tests/Binding/BindInherited.cs (Bind_Inherited_MissingValue),
    // where binding throws: the entity has a derived type the row has no place for.
    [Fact]
    public void bind_inherited_missing_value()
    {
        var diagnostics = Generate("""
            [DiscriminatorName("Type"), DiscriminatorValue(1, typeof(ConcreteDto1))]
            public class BaseDto { public int Id { get; set; } public int Type { get; set; } }
            public class ConcreteDto1 : BaseDto { }

            [MapTo(typeof(BaseDto))]
            [DiscriminatorName("Type"), DiscriminatorValue(1, typeof(ConcreteEntity1)), DiscriminatorValue(2, typeof(ConcreteEntity2))]
            public class BaseEntity { public int Id { get; set; } public int Type { get; set; } }
            public class ConcreteEntity1 : BaseEntity { }
            public class ConcreteEntity2 : BaseEntity { }
            """);

        Assert.Equal("NSG009", Assert.Single(diagnostics).Id);
    }

    // Bind_Inherited_InvalidDiscriminator: the row tells its types apart by another member.
    [Fact]
    public void bind_inherited_invalid_discriminator()
    {
        var diagnostics = Generate("""
            [DiscriminatorName("Id"), DiscriminatorValue(1, typeof(ConcreteDto1)), DiscriminatorValue(2, typeof(ConcreteDto2))]
            public class BaseDto { public int Id { get; set; } public int Type { get; set; } }
            public class ConcreteDto1 : BaseDto { }
            public class ConcreteDto2 : BaseDto { }

            [MapTo(typeof(BaseDto))]
            [DiscriminatorName("Type"), DiscriminatorValue(1, typeof(ConcreteEntity1)), DiscriminatorValue(2, typeof(ConcreteEntity2))]
            public class BaseEntity { public int Id { get; set; } public int Type { get; set; } }
            public class ConcreteEntity1 : BaseEntity { }
            public class ConcreteEntity2 : BaseEntity { }
            """);

        Assert.Equal("NSG009", Assert.Single(diagnostics).Id);
    }

    // A query cannot parse: a row member that would need it is refused, not read wrong.
    [Fact]
    public void A_projected_member_with_no_translatable_conversion_is_refused()
    {
        var diagnostics = Generate("""
            public class Row { public int Code { get; set; } }

            [MapTo(typeof(Row))]
            public class Item { public string Code { get; set; } }
            """);

        Assert.Equal("NSG006", Assert.Single(diagnostics).Id);
    }

    // Members named like the generated parameters and locals are reached through the
    // instance, never shadowed by them.
    [Fact]
    public void Members_named_like_the_generated_locals_compile()
    {
        var diagnostics = Generate("""
            public class Clash
            {
                public int Id { get; set; }
                public string source { get; set; }
                public string target { get; set; }
                public string context { get; set; }
                public string cancellationToken { get; set; }
                public string mapped { get; set; }
                public System.Collections.Generic.List<string> items0 { get; set; }
                public string @class { get; set; }
            }

            [Entity, MapFrom(typeof(Clash)), MapTo(typeof(Clash))]
            public class ClashEntity
            {
                public int Id { get; set; }
                public string source { get; set; }
                public string target { get; set; }
                public string context { get; set; }
                public string cancellationToken { get; set; }
                public string mapped { get; set; }
                public System.Collections.Generic.List<string> items0 { get; set; }
                public string @class { get; set; }
            }
            """);

        Assert.Empty(diagnostics);
    }

    // An open generic names no type a mapper could build: refused, and what else the holder
    // emits still compiles.
    [Fact]
    public void An_open_generic_target_is_refused()
    {
        var diagnostics = Generate("""
            public class Dto { public int Value { get; set; } }

            [MapFrom(typeof(Dto))]
            public class Box<T> { public int Value { get; set; } public T Item { get; set; } }

            [MapFrom(typeof(Dto))]
            public class Plain { public int Value { get; set; } }
            """);

        Assert.Equal("NSG010", Assert.Single(diagnostics).Id);
    }

    // A positional record has no parameterless constructor: refused, not emitted broken.
    [Fact]
    public void A_positional_record_target_is_refused()
    {
        var diagnostics = Generate("""
            public class Dto { public int Value { get; set; } }

            [MapFrom(typeof(Dto))]
            public record Positional(int Value);
            """);

        Assert.Equal("NSG007", Assert.Single(diagnostics).Id);
    }

    // A dictionary is written into one of its own; a query cannot build one, so a projection
    // refuses it.
    [Fact]
    public void A_dictionary_maps_and_is_refused_by_a_projection()
    {
        var diagnostics = Generate("""
            public class Dto { public System.Collections.Generic.Dictionary<string, int> Counts { get; set; } }

            [MapFrom(typeof(Dto))]
            public class Written { public System.Collections.Generic.Dictionary<string, int> Counts { get; set; } }

            public class Row { public System.Collections.Generic.Dictionary<string, long> Counts { get; set; } }

            [MapTo(typeof(Row))]
            public class Read { public System.Collections.Generic.Dictionary<string, int> Counts { get; set; } }
            """);

        Assert.Equal("NSG006", Assert.Single(diagnostics).Id);
    }

    // A row with no member in common is still a row: an empty initializer, nothing refused.
    [Fact]
    public void A_projection_with_nothing_to_read_compiles()
    {
        var diagnostics = Generate("""
            public class Row { public string Other { get; set; } }

            [MapTo(typeof(Row))]
            public class Entity1 { public int Id { get; set; } }
            """);

        Assert.Empty(diagnostics);
    }

    static IReadOnlyList<Diagnostic> Generate(string types)
    {
        var compilation = new CompilationUnitBuilder()
            .AddSource("Types.cs", $$"""
                using Microsoft.Extensions.DependencyInjection;
                using NSail.Mapping.Annotations;
                using NSail.SourceGeneration.Annotations;

                namespace Acme.Shop;

                {{types}}

                {{Holder}}
                """)
            .AddFrameworkReferences("net10.0")
            .AddContainingAssembly<Microsoft.Extensions.DependencyInjection.IServiceCollection>()
            .AddContainingAssembly<GeneratedAttribute>()
            .AddContainingAssembly<MapFromAttribute>()
            .AddContainingAssembly<MapContext>()
            .Build("MapperDiagnosticsAssembly");

        GeneratorDriver driver = CSharpGeneratorDriver.Create(new MappersSourceGenerator());

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);

        // Whatever the generator refused, what it did emit must still compile.
        Assert.Empty(output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error && !d.Id.StartsWith("NSG")));

        return diagnostics.Where(d => d.Id.StartsWith("NSG")).ToList();
    }
}
