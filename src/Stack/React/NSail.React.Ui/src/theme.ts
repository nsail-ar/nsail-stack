// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

import { alpha, createTheme, type Shadows, type Theme } from "@mui/material/styles";
import { brandMix, brandScheme, brandTypeface, brandWeights, type Brand, type Scheme } from "@nsail/stack";

interface Chrome {
  main: string;
  contrastText: string;
  secondary: string;
  /** The accent as ink on the band: the entry the reader is on. */
  accent: string;
  /** The wash under that entry. */
  selected: string;
}

// A word in the brand's hue or in a severity's: the accent and the severities are fills, and
// each of these is the ink the same pick reads in as text.
interface Inks {
  accent: string;
  error: string;
  warning: string;
  success: string;
  info: string;
}

declare module "@mui/material/styles" {
  interface Palette {
    chrome: Chrome;
    ink: Inks;
  }

  interface PaletteOptions {
    chrome?: Chrome;
    ink?: Inks;
  }

  interface TypeBackground {
    /** The rung of a surface that floats over another: a dialog, a menu. */
    raised: string;
  }
}

const { toneLean, inertWeight } = brandWeights;

// BrandMudTheme's mapping, onto MUI: the accent's states are the brand's own and handed over
// rather than letting the vendor derive a second set, and every colour the pieces draw with —
// surfaces, inks, lines, the hover and the refused fill — is the scheme's, and so are the
// shadows. The vendor's selection and focus washes and its opacities stay its own.
export function brandTheme(brand: Brand | null | undefined, dark: boolean): Theme {
  const scheme: Scheme = brandScheme(brand, dark);
  const headings = brandTypeface(brand?.typeface);
  const heading = headings ? { fontFamily: headings } : {};
  const word = (severity: string) => brandMix(scheme.textPrimary, severity, toneLean);

  return createTheme({
    palette: {
      mode: dark ? "dark" : "light",
      primary: { main: scheme.accent, light: scheme.accentHover, dark: scheme.accentPressed, contrastText: scheme.textOnAccent },
      secondary: { main: scheme.accentFocus, contrastText: scheme.textOnAccent },
      error: { main: scheme.error, contrastText: scheme.textOnError },
      warning: { main: scheme.warning, contrastText: scheme.textOnWarning },
      success: { main: scheme.success, contrastText: scheme.textOnSuccess },
      info: { main: scheme.info, contrastText: scheme.textOnInfo },
      background: { default: scheme.background, paper: scheme.surface, raised: scheme.surfaceRaised },
      text: { primary: scheme.textPrimary, secondary: scheme.textSecondary, disabled: scheme.textDisabled },
      divider: scheme.lines,
      action: {
        active: scheme.textSecondary,
        hover: scheme.rowHover,
        disabled: scheme.textDisabled,
        disabledBackground: alpha(scheme.textPrimary, inertWeight),
      },
      chrome: {
        main: scheme.chrome,
        contrastText: scheme.textOnChrome,
        secondary: scheme.textOnChromeSecondary,
        accent: scheme.accentOnChrome,
        selected: scheme.railGround,
      },
      ink: {
        accent: scheme.accentInk,
        error: word(scheme.error),
        warning: word(scheme.warning),
        success: word(scheme.success),
        info: word(scheme.info),
      },
    },
    shadows: elevations(dark),
    shape: { borderRadius: 6 },
    // A pick reaches the heading ranks only, as on the Blazor side: tables, fields and figures
    // keep the body's family.
    typography: { h1: heading, h2: heading, h3: heading, h4: heading, h5: heading, h6: heading },
    components: {
      // An outlined field's edge is a boundary, so it wears the ladder's input rung, not Lines.
      // The vendor paints primary.main as a word or a line in these places; each wears the
      // accent ink instead (ui/branding.md: the accent is a ground, never a word).
      MuiOutlinedInput: {
        styleOverrides: {
          notchedOutline: { borderColor: scheme.linesInputs },
          root: { "&.Mui-focused .MuiOutlinedInput-notchedOutline": { borderColor: scheme.accentInk } },
        },
      },
      MuiInputLabel: {
        styleOverrides: { root: { "&.Mui-focused:not(.Mui-error)": { color: scheme.accentInk } } },
      },
      MuiInput: {
        styleOverrides: { underline: { "&:after": { borderBottomColor: scheme.accentInk } } },
      },
      MuiLink: {
        styleOverrides: { root: { color: scheme.accentInk, textDecorationColor: "currentColor" } },
      },
      MuiPaper: {
        styleOverrides: { root: { backgroundImage: "none" } },
      },
      MuiDialog: {
        styleOverrides: { paper: { backgroundColor: scheme.surfaceRaised } },
      },
      MuiMenu: {
        styleOverrides: { paper: { backgroundColor: scheme.surfaceRaised } },
      },
      MuiButton: {
        defaultProps: { disableElevation: true },
        styleOverrides: {
          root: { textTransform: "none" },
        },
        // The vendor words a text or outlined primary button through these variables.
        variants: [
          {
            props: { color: "primary" },
            style: {
              "--variant-textColor": scheme.accentInk,
              "--variant-outlinedColor": scheme.accentInk,
              "--variant-outlinedBorder": scheme.accentInk,
            },
          },
        ],
      },
      MuiToggleButton: {
        styleOverrides: {
          root: { textTransform: "none", "&.MuiToggleButton-primary.Mui-selected": { color: scheme.accentInk } },
        },
      },
    },
  });
}

// BrandMudTheme.BuildElevations: the vendor's shadows vanish on a dark canvas, so each level
// casts further and darker and carries a ring, a lifted white edge in dark and the surface's
// own black edge in light, as NsTheme's ring answers per scheme.
function elevations(dark: boolean): Shadows {
  const ring = dark ? "rgba(255,255,255,.06)" : "rgba(0,0,0,.08)";
  const levels = ["none"];

  for (let level = 1; level < 25; level++) {
    const blur = Math.round((2 + level * 2.4) * 10) / 10;
    const shade = Math.round(Math.min(0.45, 0.22 + level * 0.01) * 100) / 100;

    levels.push(`0 ${1 + level}px ${blur}px rgba(0,0,0,${shade}), 0 0 0 1px ${ring}`);
  }

  return levels as Shadows;
}
