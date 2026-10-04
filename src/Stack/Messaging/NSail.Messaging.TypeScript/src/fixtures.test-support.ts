// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

import { defineEnum, defineMessage } from "./descriptors";

export const Status = defineEnum<"Open" | "Shipped">("Shop.Status", ["Open", "Shipped"]);

export interface ListOrders {
  search?: string | null;
  status?: "Open" | "Shipped" | null;
  ids?: string[];
  pageIndex?: number;
}

export const ListOrders = defineMessage<ListOrders, { items: unknown[] }>({
  name: "ListOrders",
  key: "Shop.ListOrders",
  area: "Shop",
  method: "GET",
  path: "api/shop/orders",
  fields: {
    search: { kind: "string", key: "Shop.ListOrders.Search", nullable: true, binding: "query", maxLength: 10 },
    status: { kind: "enum", key: "Shop.ListOrders.Status", nullable: true, enum: Status, binding: "query" },
    ids: { kind: "array", key: "Shop.ListOrders.Ids", element: "guid", binding: "query" },
    pageIndex: { kind: "integer", key: "Shop.ListOrders.PageIndex", binding: "query", min: 0, max: 1000 },
  },
});

export interface UpdateOrder {
  id: string;
  number: string;
  email?: string | null;
  quantity?: number;
  token?: string | null;
}

export const UpdateOrder = defineMessage<UpdateOrder, void>({
  name: "UpdateOrder",
  key: "Shop.UpdateOrder",
  area: "Shop",
  method: "PUT",
  path: "api/shop/orders/{id}",
  fields: {
    id: { kind: "guid", key: "Shop.UpdateOrder.Id", binding: "route" },
    number: { kind: "string", key: "Shop.UpdateOrder.Number", binding: "body", required: true, maxLength: 5 },
    email: { kind: "string", key: "Shop.UpdateOrder.Email", nullable: true, binding: "body", format: "email" },
    quantity: { kind: "integer", key: "Shop.UpdateOrder.Quantity", binding: "body", min: 1, max: 99 },
    token: { kind: "string", key: "Shop.UpdateOrder.Token", nullable: true, binding: "header" },
  },
});
