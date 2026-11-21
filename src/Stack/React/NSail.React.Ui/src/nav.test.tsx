// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack
// @vitest-environment happy-dom


import { act } from "react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { NsNavMenu } from "./nav";
import { clear, draw } from "./app.test-support";

afterEach(clear);

const catalog = { "Shop.Menu": "Mi cuenta", "Shop.Menu.Orders": "Pedidos", "Shop.Menu.Profile": "Perfil" };
const entries = [
  { text: "Shop.Menu.Orders", href: "/orders" },
  { text: "Shop.Menu.Profile", href: "/profile" },
];

describe("NsNavMenu", () => {
  it("names the navigation and its entries from the catalog", async () => {
    const { host } = await draw(<NsNavMenu label="Shop.Menu" entries={entries} current="/profile" />, { catalog });
    const nav = host.querySelector("nav")!;

    expect(nav.getAttribute("aria-label")).toBe("Mi cuenta");
    expect([...nav.querySelectorAll("a")].map((link) => [link.textContent, link.getAttribute("href")])).toEqual([
      ["Pedidos", "/orders"],
      ["Perfil", "/profile"],
    ]);
  });

  it("marks the entry the reader is on, and only that one", async () => {
    const { host } = await draw(<NsNavMenu label="Shop.Menu" entries={entries} current="/profile" />, { catalog });

    expect([...host.querySelectorAll("a")].map((link) => link.getAttribute("aria-current"))).toEqual([null, "page"]);
  });

  it("hands a plain click to the client's router instead of loading the page", async () => {
    const onNavigate = vi.fn();
    const { host } = await draw(<NsNavMenu label="Shop.Menu" entries={entries} onNavigate={onNavigate} />, { catalog });
    const click = new MouseEvent("click", { bubbles: true, cancelable: true, button: 0 });

    await act(async () => {
      host.querySelector("a")!.dispatchEvent(click);
    });

    expect(onNavigate).toHaveBeenCalledWith("/orders");
    expect(click.defaultPrevented).toBe(true);
  });

  it("draws a key the catalog misses as the key itself", async () => {
    const { host } = await draw(<NsNavMenu label="Shop.Menu" entries={[{ text: "Shop.Menu.Missing", href: "/x" }]} />, { catalog });

    expect(host.querySelector("a")!.textContent).toBe("Shop.Menu.Missing");
  });
});
