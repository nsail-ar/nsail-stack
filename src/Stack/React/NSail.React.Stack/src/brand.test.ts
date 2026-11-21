// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

import { describe, expect, it } from "vitest";
import { brandScheme, brandTypeface } from "./brand";
import defaults from "./brand.json";

describe("brandScheme", () => {
  // brand.json's samples are BrandTheme's own derivation (ReactBrandDefaultsTests writes them),
  // so a ladder that drifts from the C# one fails here.
  it.each(defaults.samples)("derives the ladder the C# brand derives (dark: $dark, chrome: $chrome)", ({ dark, chrome, accent, ladder }) => {
    const scheme = brandScheme({ [dark ? "dark" : "light"]: { chrome, accent } }, dark);

    expect({
      background: scheme.background,
      surface: scheme.surface,
      surfaceRaised: scheme.surfaceRaised,
      textPrimary: scheme.textPrimary,
      textSecondary: scheme.textSecondary,
      textDisabled: scheme.textDisabled,
      lines: scheme.lines,
      linesInputs: scheme.linesInputs,
    }).toEqual(ladder);
  });

  it.each(defaults.samples)("leans the accent into ink as the C# brand does (dark: $dark, accent: $accent)", ({ dark, chrome, accent, accentInk, accentOnChrome }) => {
    const scheme = brandScheme({ [dark ? "dark" : "light"]: { chrome, accent } }, dark);

    expect({ accentInk: scheme.accentInk, accentOnChrome: scheme.accentOnChrome }).toEqual({ accentInk, accentOnChrome });
  });

  it("falls back to each scheme's own default with no brand", () => {
    expect(brandScheme(undefined, false)).toMatchObject({ ...defaults.light, isDark: false });
    expect(brandScheme(null, true)).toMatchObject({ ...defaults.dark, isDark: true });
  });

  it("takes a picked hex and refuses anything else", () => {
    const scheme = brandScheme({ light: { accent: "#0a7d4f", error: "red;} body{" } }, false);

    expect(scheme.accent).toBe("#0a7d4f");
    expect(scheme.error).toBe(defaults.light.error);
    expect(brandScheme({ light: { accent: "#0a7d4f" } }, true).accent).toBe(defaults.dark.accent);
  });
});

describe("brandTypeface", () => {
  it("answers a catalog key's font stack and nothing for the house typeface", () => {
    expect(brandTypeface("work-sans")).toBe('"Work Sans", sans-serif');
    expect(brandTypeface(null)).toBeUndefined();
    expect(brandTypeface("comic-sans")).toBeUndefined();
    expect(brandTypeface("constructor")).toBeUndefined();
  });
});
