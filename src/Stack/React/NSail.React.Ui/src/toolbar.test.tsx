// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack
// @vitest-environment happy-dom

import { afterEach, describe, expect, it } from "vitest";
import { NsButton } from "./button";
import { NsToolbar } from "./toolbar";
import { clear, draw } from "./app.test-support";

afterEach(clear);

describe("NsToolbar", () => {
  it("lays its controls in one row that wraps", async () => {
    const { host } = await draw(
      <NsToolbar>
        <NsButton text="Common.Save" />
        <NsButton text="Common.Back" />
      </NsToolbar>,
      { catalog: { "Common.Save": "Guardar", "Common.Back": "Volver" } },
    );
    const row = host.querySelector("button")!.parentElement!;
    const style = getComputedStyle(row);

    expect([...row.querySelectorAll("button")].map((button) => button.textContent)).toEqual(["Guardar", "Volver"]);
    expect(style.flexDirection).toBe("row");
    expect(style.flexWrap).toBe("wrap");
  });
});
