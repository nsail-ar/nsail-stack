// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace NSail.TypeScriptGenerator.Tests;

public class TypeScriptWriterTests
{
    // An Sdk compiled from source against everything this test runs on, which is what the
    // tool receives from a real Sdk's build: its sources and its references.
    static CSharpCompilation Compile(params string[] sources)
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "Sources");
        var trees = sources
            .Append("Usings.cs")
            .Select(name => CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(directory, name)), path: name));

        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path));

        return CSharpCompilation.Create(
            "NSail.Shop.Sdk",
            trees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
    }

    static string Generate(params string[] sources)
    {
        return TypeScriptWriter.Write(SdkReader.Read(Compile(sources)));
    }

    static string Orders()
    {
        return Generate("Orders.cs", "OrdersClients.cs");
    }

    [Fact]
    public void A_route_token_binds_its_member_and_the_rest_follow_the_verb()
    {
        var text = Orders();

        Assert.Contains("path: \"api/shop/orders/{id}/lines\"", text);
        Assert.Contains("id: { kind: \"guid\", key: \"Shop.AddOrderLine.Id\", binding: \"route\" }", text);
        Assert.Contains("binding: \"body\", required: true, maxLength: 40, minLength: 2", text);
        Assert.Contains("preview: { kind: \"boolean\", key: \"Shop.AddOrderLine.Preview\", binding: \"query\" }", text);
        Assert.Contains("idempotency: { kind: \"string\", key: \"Shop.AddOrderLine.Idempotency\", nullable: true, binding: \"header\" }", text);
        Assert.Contains("search: { kind: \"string\", key: \"Shop.ListOrders.Search\", nullable: true, binding: \"query\", maxLength: 100 }", text);
    }

    [Fact]
    public void Only_what_the_message_cannot_do_without_is_mandatory_in_the_request()
    {
        var text = Orders();

        Assert.Contains("  id: string;", text);
        Assert.Contains("  product: string;", text);
        Assert.Contains("  quantity?: number;", text);
        Assert.Contains("  notifyEmail?: string | null;", text);
    }

    [Fact]
    public void Rules_are_the_message_DataAnnotations_and_a_coded_rule_is_left_to_the_server()
    {
        var text = Orders();

        Assert.Contains("quantity: { kind: \"integer\", key: \"Shop.AddOrderLine.Quantity\", binding: \"body\", min: 1, max: 99 }", text);
        Assert.Contains("format: \"email\"", text);
        Assert.Contains("discount: { kind: \"number\", key: \"Shop.AddOrderLine.Discount\", binding: \"body\", serverRules: [\"NotNegative\"] }", text);
    }

    [Fact]
    public void Results_carry_their_shapes_generics_included()
    {
        var text = Orders();

        Assert.Contains("export const ListOrders = defineMessage<ListOrders, DataPage<OrderRow>>({", text);
        Assert.Contains("export interface DataPage<T> {\n  items: T[];", text);
        Assert.Contains("export const GetTotals = defineMessage<GetTotals, Record<string, number>>({", text);
        Assert.Contains("  tags: string[];", text);
        Assert.Contains("  notes: (string | null)[];", text);
        Assert.Contains("  placedOn: string;", text);
        Assert.Contains("placedOn: { kind: \"date\", key: \"Shop.OrderRow.PlacedOn\" }", text);
        Assert.Contains("ids: { kind: \"array\", key: \"Shop.ListOrders.Ids\", element: \"guid\", binding: \"query\" }", text);
    }

    [Fact]
    public void An_enum_crosses_as_its_names_with_its_key()
    {
        var text = Orders();

        Assert.Contains("export type OrderStatus = \"Open\" | \"Shipped\";", text);
        Assert.Contains("export const OrderStatus = defineEnum<OrderStatus>(\"Shop.OrderStatus\", [\"Open\", \"Shipped\"]);", text);
        Assert.Contains("status: { kind: \"enum\", key: \"Shop.ListOrders.Status\", nullable: true, enum: OrderStatus, binding: \"query\" }", text);
    }

    [Fact]
    public void Enums_are_declared_before_anything_that_reads_them()
    {
        var text = Orders();

        Assert.True(text.IndexOf("export const OrderStatus", StringComparison.Ordinal) < text.IndexOf("export const OrderRow", StringComparison.Ordinal));
    }

    [Fact]
    public void Two_types_with_one_name_are_refused()
    {
        var exception = Assert.Throws<SdkReadException>(() => Generate("Twins.cs", "OrdersClients.cs"));

        Assert.Contains("NSail.Shop.Left.Twin", exception.Message);
        Assert.Contains("NSail.Shop.Right.Twin", exception.Message);
    }

    [Fact]
    public void A_route_token_with_no_member_is_refused_as_the_C_sharp_generator_refuses_it()
    {
        var exception = Assert.Throws<SdkReadException>(() => Generate("Unbound.cs", "OrdersClients.cs"));

        Assert.Contains("NSG001", exception.Message);
    }

    [Fact]
    public void An_Sdk_with_no_client_holder_names_no_message_and_is_refused()
    {
        var exception = Assert.Throws<SdkReadException>(() => Generate("Orders.cs"));

        Assert.Contains("Http.Clients", exception.Message);
    }

    [Fact]
    public void A_Stack_subclass_of_a_BCL_rule_is_read_as_the_bound_it_fixes()
    {
        var text = Orders();

        Assert.Contains("search: { kind: \"string\", key: \"Shop.ListOrders.Search\", nullable: true, binding: \"query\", maxLength: 100 }", text);
    }

    [Fact]
    public void An_Sdk_that_does_not_compile_is_refused_instead_of_read_as_empty()
    {
        var exception = Assert.Throws<SdkReadException>(() => Generate("Orders.cs", "OrdersClients.cs", "Broken.cs"));

        Assert.Contains("TypeNobodyDeclared", exception.Message);
    }
}
