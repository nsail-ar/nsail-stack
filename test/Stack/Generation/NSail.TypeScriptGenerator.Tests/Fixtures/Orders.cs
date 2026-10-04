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

    [EmailAddress]
    public string? NotifyEmail { get; set; }

    [AsQuery]
    public bool Preview { get; set; }

    [AsHeader]
    public string? Idempotency { get; set; }
}

[Http(Get, "api/shop/totals")]
public class GetTotals : IMessage<Dictionary<string, decimal>>
{
}

[Http(Get, "api/shop/orders/{id}")]
public class GetOrderWithoutId : IMessage<OrderRow>
{
}
