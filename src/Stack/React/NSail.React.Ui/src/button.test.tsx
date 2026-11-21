// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack
// @vitest-environment happy-dom

import { afterEach, describe, expect, it } from "vitest";
import { NsButton } from "./button";
import { clear, draw } from "./app.test-support";

afterEach(clear);

const catalog = { "Shop.Pay": "Pagar" };

describe("NsButton", () => {
  it("names its act from the catalog", async () => {
    const { host } = await draw(<NsButton text="Shop.Pay" />, { catalog });

    expect(host.querySelector("button")!.textContent).toBe("Pagar");
  });

  // ui/actions.md: an act in flight keeps its word beside the progress glyph.
  it("keeps its word beside the progress while busy, and refuses a second press", async () => {
    const { host } = await draw(<NsButton text="Shop.Pay" variant="primary" busy />, { catalog });
    const button = host.querySelector("button")!;

    expect(button.textContent).toContain("Pagar");
    expect(button.querySelector("[role=progressbar]")).not.toBeNull();
    expect(button.className).toContain("MuiButton-loadingPositionStart");
    expect(button.disabled).toBe(true);
    expect(button.getAttribute("aria-busy")).toBe("true");
  });
});
