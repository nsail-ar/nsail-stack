// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using NSail.Shop.Left;
using NSail.Shop.Orders;
using NSail.Shop.Right;

namespace NSail.TypeScriptGenerator.Tests;

public class TypeScriptWriterTests
{
    static string Generate(params Type[] messages)
    {
        return TypeScriptWriter.Write(SdkReader.Read(messages, ["Fixtures"]));
    }

    [Fact]
    public void A_route_token_binds_its_member_and_the_rest_follow_the_verb()
    {
        var text = Generate(typeof(AddOrderLine), typeof(ListOrders));

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
        var text = Generate(typeof(AddOrderLine));

        Assert.Contains("  id: string;", text);
        Assert.Contains("  product: string;", text);
        Assert.Contains("  quantity?: number;", text);
        Assert.Contains("  notifyEmail?: string | null;", text);
    }

    [Fact]
    public void Rules_are_the_message_DataAnnotations_and_a_coded_rule_is_left_to_the_server()
    {
        var text = Generate(typeof(AddOrderLine));

        Assert.Contains("quantity: { kind: \"integer\", key: \"Shop.AddOrderLine.Quantity\", binding: \"body\", min: 1, max: 99 }", text);
        Assert.Contains("format: \"email\"", text);
        Assert.Contains("discount: { kind: \"number\", key: \"Shop.AddOrderLine.Discount\", binding: \"body\", codes: [\"NotNegative\"] }", text);
    }

    [Fact]
    public void Results_carry_their_shapes_generics_included()
    {
        var text = Generate(typeof(ListOrders), typeof(GetTotals));

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
        var text = Generate(typeof(ListOrders));

        Assert.Contains("export type OrderStatus = \"Open\" | \"Shipped\";", text);
        Assert.Contains("export const OrderStatus = defineEnum<OrderStatus>(\"Shop.OrderStatus\", [\"Open\", \"Shipped\"]);", text);
        Assert.Contains("status: { kind: \"enum\", key: \"Shop.ListOrders.Status\", nullable: true, enum: OrderStatus, binding: \"query\" }", text);
    }

    [Fact]
    public void Enums_are_declared_before_anything_that_reads_them()
    {
        var text = Generate(typeof(ListOrders));

        Assert.True(text.IndexOf("export const OrderStatus", StringComparison.Ordinal) < text.IndexOf("export const OrderRow", StringComparison.Ordinal));
    }

    [Fact]
    public void Two_types_with_one_name_are_refused()
    {
        var exception = Assert.Throws<SdkReadException>(() => Generate(typeof(GetLeft), typeof(GetRight)));

        Assert.Contains(typeof(NSail.Shop.Left.Twin).FullName!, exception.Message);
        Assert.Contains(typeof(NSail.Shop.Right.Twin).FullName!, exception.Message);
    }

    [Fact]
    public void A_route_token_with_no_member_is_refused()
    {
        var exception = Assert.Throws<SdkReadException>(() => Generate(typeof(GetOrderWithoutId)));

        Assert.Contains("id", exception.Message);
    }
}
