// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack
// @vitest-environment happy-dom

import { afterEach, describe, expect, it, vi } from "vitest";
import { act } from "react";
import { brandMix, brandScheme, brandWeights, defineEnum, defineMessage, useForm, type Brand, type Field } from "@nsail/stack";
import { NsButton } from "./button";
import { NsChip } from "./chip";
import { NsForm, NsSaved } from "./form";
import { NsNavMenu } from "./nav";
import { NsRadioGroup } from "./radio";
import { useConfirm } from "./dialog";
import { clear, draw } from "./app.test-support";

afterEach(async () => {
  await clear();
  vi.unstubAllGlobals();
});

// The device's answer to prefers-color-scheme, as the browser gives it.
function device(dark: boolean) {
  vi.stubGlobal("matchMedia", (query: string) => ({
    matches: dark && query.includes("dark"),
    media: query,
    addEventListener: () => {},
    removeEventListener: () => {},
    addListener: () => {},
    removeListener: () => {},
    onchange: null,
    dispatchEvent: () => false,
  }));
}

// The colour a piece is painted in, read off the page as the DOM resolves the cascade (happy-dom
// answers it as the stylesheet wrote it).
function fill(element: Element): string {
  return getComputedStyle(element).backgroundColor.toLowerCase();
}

function ink(element: Element): string {
  return getComputedStyle(element).color.toLowerCase();
}

function Asks() {
  const confirm = useConfirm();

  return <button id="ask" onClick={() => void confirm("Common.Error")} />;
}

const Ping = defineMessage<Record<never, never>, void>({
  name: "Ping",
  key: "Shop.Ping",
  area: "Shop",
  method: "POST",
  path: "api/shop/ping",
  fields: {},
});

function Saved() {
  const form = useForm(Ping, {});

  return (
    <NsForm form={form} onSubmit={async () => {}}>
      <button id="send" type="submit" />
      <NsSaved />
    </NsForm>
  );
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

  it("follows the device's scheme when none is named", async () => {
    device(true);

    const { host } = await draw(<NsButton text="Common.Save" variant="primary" />, { brand: green, dark: null });

    expect(fill(host.querySelector("button")!)).toBe("#5fd39c");
  });

  it("draws the light scheme on a light device when none is named", async () => {
    device(false);

    const { host } = await draw(<NsButton text="Common.Save" variant="primary" />, { brand: green, dark: null });

    expect(fill(host.querySelector("button")!)).toBe("#0a7d4f");
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

  // ui/branding.md: the accent is a ground, and a word in the brand's hue wears the ink the
  // C# brand derives for it (BrandTheme.AccentInk, AccentOnChrome).
  it("words a quiet NsButton in the accent ink, not the accent", async () => {
    const { host } = await draw(<NsButton text="Common.Back" variant="quiet" />);
    const scheme = brandScheme(undefined, false);

    expect(scheme.accentInk).not.toBe(scheme.accent.toLowerCase());
    expect(ink(host.querySelector("button")!)).toBe(scheme.accentInk);
  });

  it("words the chosen face of an NsRadioGroup in the accent ink", async () => {
    const field: Field = { kind: "enum", key: "Shop.Period", enum: defineEnum("Shop.Period", ["a", "b"]) };
    const { host } = await draw(<NsRadioGroup field={field} value="a" onChange={() => {}} />);

    expect(ink(host.querySelector("[aria-pressed=true]")!)).toBe(brandScheme(undefined, false).accentInk);
  });

  it("marks the navigation entry the reader is on in the accent on the chrome, over the rail's wash", async () => {
    const brand: Brand = { light: { accent: "#f9b062" } };
    const { host } = await draw(
      <NsNavMenu label="Common.Menu" current="/a" entries={[{ text: "Common.Save", href: "/a" }, { text: "Common.Back", href: "/b" }]} />,
      { brand },
    );
    const scheme = brandScheme(brand, false);
    const here = host.querySelector("[aria-current=page]")!;

    expect(ink(here)).toBe(scheme.accentOnChrome);
    expect(fill(here)).toBe(scheme.railGround);
  });

  it("words an outlined state chip in its severity's ink", async () => {
    const { host } = await draw(<NsChip text="Common.Error" severity="warning" />);
    const scheme = brandScheme(undefined, false);

    expect(ink(host.querySelector(".MuiChip-root")!)).toBe(brandMix(scheme.textPrimary, scheme.warning, brandWeights.toneLean));
  });

  it("words the form's Saved in success's ink, not the severity", async () => {
    const brand: Brand = { light: { success: "#66bb6a" } };
    const { host } = await draw(<Saved />, { brand });

    await act(async () => host.querySelector<HTMLButtonElement>("#send")!.click());

    const scheme = brandScheme(brand, false);
    const word = host.querySelector("[role=status]")!;

    expect(word.textContent).toBe("Guardado");
    expect(ink(word)).toBe(brandMix(scheme.textPrimary, scheme.success, brandWeights.toneLean));
  });

  it("casts the confirmation's shadow with the dark scheme's lifted ring", async () => {
    const { host } = await draw(<Asks />, { dark: true });

    host.querySelector<HTMLButtonElement>("#ask")!.click();

    // BuildElevations' level 8.
    expect(getComputedStyle(document.querySelector("dialog")!).boxShadow.replace(/,\s*/g, ",")).toBe(
      "0 9px 21.2px rgba(0,0,0,0.3),0 0 0 1px rgba(255,255,255,.06)",
    );
  });

  it("raises the confirmation onto the scheme's raised rung", async () => {
    const { host } = await draw(<Asks />, { dark: true });

    host.querySelector<HTMLButtonElement>("#ask")!.click();

    expect(fill(document.querySelector("dialog")!)).toBe(brandScheme(undefined, true).surfaceRaised);
  });
});
