// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack
// @vitest-environment happy-dom

import { afterEach, describe, expect, it } from "vitest";
import { brandScheme, type Brand } from "@nsail/stack";
import { NsButton } from "./button";
import { NsChip } from "./chip";
import { clear, draw } from "./app.test-support";

afterEach(clear);

// The colour a piece is painted in, read off the page as the DOM resolves the cascade (happy-dom
// answers it as the stylesheet wrote it).
function fill(element: Element): string {
  return getComputedStyle(element).backgroundColor.toLowerCase();
}

const green: Brand = { light: { accent: "#0a7d4f" }, dark: { accent: "#5fd39c", error: "#ff8a80" } };

describe("the theme a Brand lays", () => {
  it("draws a primary NsButton in the light scheme's accent", async () => {
    const { host } = await draw(<NsButton text="Common.Save" variant="primary" />, { brand: green });

    expect(fill(host.querySelector("button")!)).toBe("#0a7d4f");
  });

  it("draws it in the dark scheme's accent when the scheme is dark", async () => {
    const { host } = await draw(<NsButton text="Common.Save" variant="primary" />, { brand: green, dark: true });

    expect(fill(host.querySelector("button")!)).toBe("#5fd39c");
  });

  it("draws it in the Stack's own accent with no Brand", async () => {
    const { host } = await draw(<NsButton text="Common.Save" variant="primary" />);

    expect(fill(host.querySelector("button")!)).toBe(brandScheme(undefined, false).accent.toLowerCase());
  });

  it("reaches the status colours: a solid error chip wears the scheme's error", async () => {
    const { host } = await draw(<NsChip text="Common.Error" severity="error" solid />, { brand: green, dark: true });

    expect(fill(host.querySelector(".MuiChip-root")!)).toBe("#ff8a80");
  });

  it("lays the page's canvas in the scheme's ladder", async () => {
    await draw(<span />, { brand: green, dark: true });

    expect(fill(document.body)).toBe(brandScheme(green, true).background);
  });
});
