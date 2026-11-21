// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

import defaults from "./brand.json";

/** One colour scheme of a {@link Brand}, as the wire carries `NSail.Components.BrandTheme`: hex
 * strings, only what a tenant picked. */
export interface BrandTheme {
  logo?: string | null;
  accent: string;
  accentHover: string;
  accentPressed: string;
  accentFocus: string;
  textOnAccent: string;
  chrome: string;
  textOnChrome: string;
  textOnChromeSecondary: string;
  isDark: boolean;
  rowHover: string;
  error: string;
  warning: string;
  success: string;
  info: string;
  textOnError: string;
  textOnWarning: string;
  textOnSuccess: string;
  textOnInfo: string;
}

/** An install's branding, as the wire carries `NSail.Components.Brand`. A scheme names only what
 * it changes; every slot left out, or not a hex, is that scheme's own default. */
export interface Brand {
  name?: string;
  /** A key of the house's typeface catalog, or none for the house typeface. */
  typeface?: string | null;
  defaultDark?: boolean;
  light?: Partial<BrandTheme> | null;
  dark?: Partial<BrandTheme> | null;
}

/** The surface and ink ladder a scheme derives from its chrome. */
export interface Ladder {
  background: string;
  surface: string;
  surfaceRaised: string;
  textPrimary: string;
  textSecondary: string;
  textDisabled: string;
  lines: string;
  linesInputs: string;
}

/** The accent as ink, derived at paint as `BrandTheme.AccentInk`/`AccentOnChrome` are: the
 * accent itself is a ground and never a word. */
export interface AccentInks {
  /** The accent as ink on the content's surfaces: a link, a quiet button, the focused field. */
  accentInk: string;
  /** The accent as ink on the chrome: the navigation entry the reader is on. */
  accentOnChrome: string;
  /** The wash the navigation marks the reader's entry with. */
  railGround: string;
}

/** A scheme ready to paint: its picks checked, its ladder and its accent inks derived. */
export type Scheme = BrandTheme & Ladder & AccentInks;

// The defaults are BrandTheme.DefaultLight()/DefaultDark() and Typefaces.All as the C# side
// serializes them; ReactBrandDefaultsTests fails when the file and the classes disagree.
const schemes: Record<"light" | "dark", BrandTheme> = { light: defaults.light, dark: defaults.dark };
const typefaces: Record<string, string[]> = defaults.typefaces;

// BrandTheme's ladder masks: each rung keeps the luminance written here and wears the chrome's
// hue. The derivation is pinned to the C# one by brand.test.ts over the samples in brand.json.
const masks: Record<"light" | "dark", Ladder> = {
  dark: {
    background: "#161513",
    surface: "#1c1a18",
    surfaceRaised: "#201f1c",
    textPrimary: "#e7e4dd",
    textSecondary: "#9c9992",
    textDisabled: "#65635f",
    lines: "#312f2b",
    linesInputs: "#726f69",
  },
  light: {
    background: "#fcfbfa",
    surface: "#ffffff",
    surfaceRaised: "#ffffff",
    textPrimary: "#262624",
    textSecondary: "#6e6b66",
    textDisabled: "#9a9792",
    lines: "#d0cdc8",
    linesInputs: "#757575",
  },
};

// BrandTone's weights and the lean AccentInk walks, from the fullest down.
const washWeight = 0.13;
const railWeight = 0.08;
const fullLean = 11;
const leanSteps = 20;
const bodyContrast = 4.5;
const surfaceTint = 0.03;
const inkTint = 0.02;
const hex = /^#([0-9a-fA-F]{3}|[0-9a-fA-F]{4}|[0-9a-fA-F]{6}|[0-9a-fA-F]{8})$/;

/** One scheme of a brand as it may reach a stylesheet (`Brand.Checked()`): with no brand, or a
 * slot that is not a hex, the scheme's own default. */
export function brandScheme(brand: Brand | null | undefined, dark: boolean): Scheme {
  const fallback = schemes[dark ? "dark" : "light"];
  const picked: Partial<BrandTheme> = (dark ? brand?.dark : brand?.light) ?? {};
  const checked = { ...fallback, logo: picked.logo ?? null, isDark: dark } as BrandTheme;

  for (const slot of Object.keys(fallback) as (keyof BrandTheme)[]) {
    const value = picked[slot];

    if (slot !== "logo" && slot !== "isDark" && typeof value === "string" && hex.test(value)) {
      (checked as unknown as Record<string, string>)[slot] = value;
    }
  }

  const mask = masks[dark ? "dark" : "light"];
  const ladder: Ladder = {
    background: tint(mask.background, checked.chrome, surfaceTint),
    surface: tint(mask.surface, checked.chrome, surfaceTint),
    surfaceRaised: tint(mask.surfaceRaised, checked.chrome, surfaceTint),
    textPrimary: tint(mask.textPrimary, checked.chrome, inkTint),
    textSecondary: tint(mask.textSecondary, checked.chrome, inkTint),
    textDisabled: tint(mask.textDisabled, checked.chrome, inkTint),
    lines: tint(mask.lines, checked.chrome, surfaceTint),
    linesInputs: tint(mask.linesInputs, checked.chrome, surfaceTint),
  };
  const grounds = [ladder.background, ladder.surface, ladder.surfaceRaised].flatMap((rung) => [rung, brandMix(rung, checked.accent, washWeight)]);
  const railGround = brandMix(checked.chrome, checked.textOnChrome, railWeight);

  return {
    ...checked,
    ...ladder,
    accentInk: accentInk(checked.accent, ladder.textPrimary, grounds),
    accentOnChrome: accentInk(checked.accent, checked.textOnChrome, [checked.chrome, railGround]),
    railGround,
  };
}

/** `BrandTone.Mix`: `from` moved toward `to` by `weight`; `from` itself when either does not
 * parse. */
export function brandMix(from: string, to: string, weight: number): string {
  const start = parse(from);
  const end = parse(to);

  if (!start || !end) {
    return from;
  }

  return `#${start.map((channel, index) => hexByte(roundEven(channel + (end[index] - channel) * weight))).join("")}`;
}

/** The font stack of a typeface key (`Typefaces.Find`), or undefined for the house typeface. */
export function brandTypeface(key: string | null | undefined): string | undefined {
  const family = key && Object.hasOwn(typefaces, key) ? typefaces[key] : undefined;

  return family?.map((name) => (name.includes(" ") ? `"${name}"` : name)).join(", ");
}

// BrandTone.AccentInk: the ink leaned toward the accent as far as clears AA on every ground at
// once, and the plain ink where no lean does.
function accentInk(accent: string, ink: string, grounds: string[]): string {
  for (let lean = fullLean; lean > 0; lean--) {
    const candidate = brandMix(ink, accent, lean / leanSteps);

    if (grounds.every((ground) => contrast(ground, candidate) >= bodyContrast)) {
      return candidate;
    }
  }

  return ink;
}

function contrast(first: string, second: string): number {
  const one = luminance(first);
  const other = luminance(second);

  return (Math.max(one, other) + 0.05) / (Math.min(one, other) + 0.05);
}

function luminance(color: string): number {
  const channels = parse(color);

  return channels ? relative(channels) : 0;
}

// .NET's Math.Round, which Blend uses: a half goes to the even neighbour.
function roundEven(value: number): number {
  const rounded = Math.round(value);

  return Math.abs(value % 1) === 0.5 && rounded % 2 !== 0 ? rounded - 1 : rounded;
}

function hexByte(value: number): string {
  return value.toString(16).padStart(2, "0");
}

// BrandTone.Tint: the rung keeps its relative luminance; hue and chroma are the chrome's in
// OKLab, the chroma capped and backed off until the rung fits sRGB.
function tint(rung: string, chrome: string, cap: number): string {
  const mask = parse(rung);
  const band = parse(chrome);

  if (!mask || !band) {
    return rung;
  }

  const target = relative(mask);
  const [, a, b] = toOklab(band);
  const chroma = Math.sqrt(a * a + b * b);
  let scale = chroma > 0.002 ? Math.min(chroma, cap) / chroma : 0;

  for (;;) {
    const lightness = solve(target, a * scale, b * scale);

    if (scale < 0.01 || inGamut(lightness, a * scale, b * scale)) {
      return `#${fromOklab(lightness, a * scale, b * scale)
        .map(hexByte)
        .join("")}`;
    }

    scale *= 0.85;
  }
}

function solve(target: number, a: number, b: number): number {
  let low = 0;
  let high = 1;

  for (let step = 0; step < 40; step++) {
    const middle = (low + high) / 2;

    if (relative(fromOklab(middle, a, b)) < target) {
      low = middle;
    } else {
      high = middle;
    }
  }

  return (low + high) / 2;
}

function relative(channels: number[]): number {
  return 0.2126 * linear(channels[0]) + 0.7152 * linear(channels[1]) + 0.0722 * linear(channels[2]);
}

function toOklab(channels: number[]): [number, number, number] {
  const [red, green, blue] = channels.map(linear);
  const l = Math.cbrt(0.4122214708 * red + 0.5363325363 * green + 0.0514459929 * blue);
  const m = Math.cbrt(0.2119034982 * red + 0.6806995451 * green + 0.1073969566 * blue);
  const s = Math.cbrt(0.0883024619 * red + 0.2817188376 * green + 0.6299787005 * blue);

  return [
    0.2104542553 * l + 0.793617785 * m - 0.0040720468 * s,
    1.9779984951 * l - 2.428592205 * m + 0.4505937099 * s,
    0.0259040371 * l + 0.7827717662 * m - 0.808675766 * s,
  ];
}

function fromOklab(lightness: number, a: number, b: number): number[] {
  return toLinear(lightness, a, b).map(encode);
}

function inGamut(lightness: number, a: number, b: number): boolean {
  const slack = 0.001;

  return toLinear(lightness, a, b).every((channel) => channel >= -slack && channel <= 1 + slack);
}

function toLinear(lightness: number, a: number, b: number): [number, number, number] {
  const l = (lightness + 0.3963377774 * a + 0.2158037573 * b) ** 3;
  const m = (lightness - 0.1055613458 * a - 0.0638541728 * b) ** 3;
  const s = (lightness - 0.0894841775 * a - 1.291485548 * b) ** 3;

  return [
    4.0767416621 * l - 3.3077115913 * m + 0.2309699292 * s,
    -1.2684380046 * l + 2.6097574011 * m - 0.3413193965 * s,
    -0.0041960863 * l - 0.7034186147 * m + 1.707614701 * s,
  ];
}

function encode(value: number): number {
  const clamped = Math.min(1, Math.max(0, value));
  const gamma = clamped <= 0.0031308 ? clamped * 12.92 : 1.055 * clamped ** (1 / 2.4) - 0.055;

  return Math.round(gamma * 255);
}

function linear(channel: number): number {
  const value = channel / 255;

  return value <= 0.03928 ? value / 12.92 : ((value + 0.055) / 1.055) ** 2.4;
}

function parse(color: string): number[] | null {
  const digits = color.replace(/^#+/, "");
  const wide = digits.length === 6 || digits.length === 8;

  if (!wide && digits.length !== 3 && digits.length !== 4) {
    return null;
  }

  const channels = [0, 1, 2].map((channel) => {
    const pair = wide ? digits.slice(channel * 2, channel * 2 + 2) : digits[channel] + digits[channel];

    return /^[0-9a-fA-F]{2}$/.test(pair) ? parseInt(pair, 16) : NaN;
  });

  return channels.some(Number.isNaN) ? null : channels;
}
