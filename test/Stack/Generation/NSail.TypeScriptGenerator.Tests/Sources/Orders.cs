// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.ComponentModel.DataAnnotations;
using NSail.Messaging;
using NSail.Messaging.Runtime.Validation;
using NSail.Paging;

namespace NSail.Shop.Orders;

public enum OrderStatus
{
    Open,
    Shipped
}

public enum NoStatus
{
}

[Flags]
public enum Perks
{
    None = 0,
    Gift = 1,
    Rush = 2
}

public class Extras
{
    [System.Text.Json.Serialization.JsonPropertyName("first-name")]
    public string FirstName { get; set; } = string.Empty;

    public Uri? Link { get; set; }

    public Version Build { get; set; } = new();

    public System.Text.Json.JsonElement Raw { get; set; }

    public System.Text.Json.Nodes.JsonNode? Node { get; set; }

    public System.Numerics.BigInteger Big { get; set; }

    public Int128 Wide { get; set; }

    public NoStatus Empty { get; set; }

    public Perks Perks { get; set; }
}

[Http(Get, "api/shop/extras")]
public class GetExtras : IMessage<Extras>
{
}

public class OrderRow
{
    public Guid Id { get; set; }

    public string Number { get; set; } = string.Empty;

    public string? Customer { get; set; }

    public OrderStatus Status { get; set; }

    public DateOnly PlacedOn { get; set; }

    public decimal Total { get; set; }

    public List<string> Tags { get; set; } = [];

    public List<string?> Notes { get; set; } = [];
}

[Http(Get, "api/shop/orders")]
public class ListOrders : PagedMessage, IMessage<DataPage<OrderRow>>
{
    [SearchTerm]
    public string? Search { get; set; }

    public OrderStatus? Status { get; set; }

    public Guid[] Ids { get; set; } = [];
}

[Http(Post, "api/shop/orders/{id}/lines")]
public class AddOrderLine : IMessage
{
    public required Guid Id { get; set; }

    [Required]
    [StringLength(40, MinimumLength = 2)]
    public string Product { get; set; } = string.Empty;

    [Range(1, 99)]
    public int Quantity { get; set; } = 1;

    [NotNegative]
    public decimal Discount { get; set; }

    [RegularExpression(@"^[A-Z]{2}-[0-9]{4}$")]
    public string Code { get; set; } = string.Empty;

    [RegularExpression(@"(?i)^[a-z]+$")]
    public string Slug { get; set; } = string.Empty;

    [RegularExpression(@"^\++$")]
    public string Plus { get; set; } = string.Empty;

    [RegularExpression(@"^C:\\Archive$")]
    public string WinPath { get; set; } = string.Empty;

    [RegularExpression(@"^[*+]+$")]
    public string StarClass { get; set; } = string.Empty;

    [RegularExpression(@"^[?+]+$")]
    public string QuestClass { get; set; } = string.Empty;

    [RegularExpression(@"^[{}+]+$")]
    public string BraceClass { get; set; } = string.Empty;

    [RegularExpression(@"\Aabc\z")]
    public string Anchored { get; set; } = string.Empty;

    [RegularExpression(@"^(?>a+)b$")]
    public string Atomic { get; set; } = string.Empty;

    [RegularExpression(@"^a++$")]
    public string Possessive { get; set; } = string.Empty;

    [RegularExpression(@"^[a-z-[aeiou]]+$")]
    public string Subtracted { get; set; } = string.Empty;

    public long Batch { get; set; }

    public ulong? Serial { get; set; }

    [EmailAddress]
    public string? NotifyEmail { get; set; }

    [AsQuery]
    public bool Preview { get; set; }

    [AsQuery]
    [System.Text.Json.Serialization.JsonPropertyName("page-size")]
    public int PageSize { get; set; }

    [AsHeader]
    public string? Idempotency { get; set; }
}

[Http(Get, "api/shop/totals")]
public class GetTotals : IMessage<Dictionary<string, decimal>>
{
}
