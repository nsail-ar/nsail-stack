// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack
// @vitest-environment happy-dom


import { act } from "react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { NsChip } from "./chip";
import { NsList, NsListItem } from "./list";
import { clear, draw } from "./app.test-support";

afterEach(clear);

describe("NsListItem", () => {
  it("draws its data in a list row, with the aside's translated state", async () => {
    const { host } = await draw(
      <NsList>
        <NsListItem primary="Firefox en Linux" secondary="Buenos Aires" aside={<NsChip text="Shop.Session.Current" severity="success" />} />
      </NsList>,
      { catalog: { "Shop.Session.Current": "Esta sesión" } },
    );
    const row = host.querySelector("ul > li")!;

    expect(row.textContent).toContain("Firefox en Linux");
    expect(row.textContent).toContain("Buenos Aires");
    expect(row.textContent).toContain("Esta sesión");
  });

  it("opens by its address as a link", async () => {
    const { host } = await draw(
      <NsList>
        <NsListItem primary="Pedido 12" href="/orders/12" />
      </NsList>,
    );

    expect(host.querySelector("li a")!.getAttribute("href")).toBe("/orders/12");
  });

  it("opens by its act as a button, from the keyboard too", async () => {
    const onClick = vi.fn();
    const { host } = await draw(
      <NsList>
        <NsListItem primary="Pedido 12" onClick={onClick} />
      </NsList>,
    );
    const row = host.querySelector<HTMLElement>("li [role=button]")!;

    await act(async () => row.click());

    expect(onClick).toHaveBeenCalledTimes(1);
    expect(row.tabIndex).toBe(0);
  });
});
