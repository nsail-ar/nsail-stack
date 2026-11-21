// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack
// @vitest-environment happy-dom

import { afterEach, describe, expect, it } from "vitest";
import { defineModel } from "@nsail/stack";
import { NsButton } from "./button";
import { NsEmpty } from "./empty";
import { NsTable } from "./table";
import { clear, draw } from "./app.test-support";

afterEach(clear);

describe("NsEmpty", () => {
  it("says the region is empty in the catalog's words, with the act that fills it", async () => {
    const { host } = await draw(<NsEmpty text="Shop.Orders.Empty" action={<NsButton text="Common.New" variant="primary" />} />, {
      catalog: { "Shop.Orders.Empty": "Todavía no hiciste pedidos" },
    });
    const region = host.querySelector("[role=status]")!;

    expect(region.textContent).toContain("Todavía no hiciste pedidos");
    expect(region.querySelector("button")!.textContent).toBe("Nuevo");
  });

  it("is what an NsTable draws for an answer with no rows", async () => {
    const Row = defineModel<{ id: string }>("Shop.OrderRow", { id: { kind: "string", key: "Shop.OrderRow.Id" } });
    const { host } = await draw(<NsTable model={Row} rows={[]} rowKey="id" columns={["id"]} empty="Shop.Orders.Empty" />, {
      catalog: { "Shop.Orders.Empty": "Todavía no hiciste pedidos" },
    });

    expect(host.querySelector("table")).toBeNull();
    expect(host.querySelector("[role=status]")!.textContent).toBe("Todavía no hiciste pedidos");
  });
});
