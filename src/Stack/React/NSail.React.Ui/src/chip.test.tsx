// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack
// @vitest-environment happy-dom

import { afterEach, describe, expect, it } from "vitest";
import { NsChip } from "./chip";
import { clear, draw } from "./app.test-support";

afterEach(clear);

describe("NsChip", () => {
  it("names the state in the catalog's word", async () => {
    const { host } = await draw(<NsChip text="Shop.State.Paid" severity="success" />, { catalog: { "Shop.State.Paid": "Pagado" } });

    expect(host.querySelector(".MuiChip-root")!.textContent).toBe("Pagado");
  });

  it.each([
    [undefined, false, "MuiChip-outlined", "MuiChip-colorDefault"],
    ["warning", false, "MuiChip-outlined", "MuiChip-colorWarning"],
    ["warning", true, "MuiChip-filled", "MuiChip-colorWarning"],
  ] as const)("wears the tone (severity: %s, solid: %s)", async (severity, solid, face, tone) => {
    const { host } = await draw(<NsChip text="Shop.State.Paid" severity={severity} solid={solid} />);
    const chip = host.querySelector(".MuiChip-root")!;

    expect(chip.classList).toContain(face);
    expect(chip.classList).toContain(tone);
  });
});
