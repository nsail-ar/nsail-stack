// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack
// @vitest-environment happy-dom


import { afterEach, describe, expect, it } from "vitest";
import { NsCard } from "./card";
import { NsChip } from "./chip";
import { clear, draw } from "./app.test-support";

afterEach(clear);

const catalog = { "Shop.Profile.Title": "Tus datos", "Shop.Profile.Subtitle": "Así te ve la tienda", "Shop.State.Active": "Activa" };

describe("NsCard", () => {
  it("heads its section with the translated title, the line under it and its aside", async () => {
    const { host } = await draw(
      <NsCard title="Shop.Profile.Title" subtitle="Shop.Profile.Subtitle" aside={<NsChip text="Shop.State.Active" />}>
        <p id="content" />
      </NsCard>,
      { catalog },
    );
    const section = host.querySelector("section")!;

    expect(section.querySelector("h2")!.textContent).toBe("Tus datos");
    expect(section.textContent).toContain("Así te ve la tienda");
    expect(section.textContent).toContain("Activa");
    expect(section.querySelector("#content")).not.toBeNull();
  });

  it("draws no heading without a title", async () => {
    const { host } = await draw(<NsCard>body</NsCard>, { catalog });

    expect(host.querySelector("section h2")).toBeNull();
    expect(host.querySelector("section")!.textContent).toBe("body");
  });
});
