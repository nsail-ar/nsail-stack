// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

import { createTheme, type Theme } from "@mui/material/styles";
import { brandScheme, brandTypeface, type Brand, type Scheme } from "@nsail/stack";

declare module "@mui/material/styles" {
  interface Palette {
    chrome: { main: string; contrastText: string; secondary: string };
  }

  interface PaletteOptions {
    chrome?: { main: string; contrastText: string; secondary: string };
  }
}

// BrandMudTheme's mapping, onto MUI: the accent's states are the brand's own and handed over
// rather than letting the vendor derive a second set, and every surface and ink is the scheme's
// ladder, so no vendor default reaches the screen.
export function brandTheme(brand: Brand | null | undefined, dark: boolean): Theme {
  const scheme: Scheme = brandScheme(brand, dark);
  const headings = brandTypeface(brand?.typeface);
  const heading = headings ? { fontFamily: headings } : {};

  return createTheme({
    palette: {
      mode: dark ? "dark" : "light",
      primary: { main: scheme.accent, light: scheme.accentHover, dark: scheme.accentPressed, contrastText: scheme.textOnAccent },
      secondary: { main: scheme.accentFocus, contrastText: scheme.textOnAccent },
      error: { main: scheme.error, contrastText: scheme.textOnError },
      warning: { main: scheme.warning, contrastText: scheme.textOnWarning },
      success: { main: scheme.success, contrastText: scheme.textOnSuccess },
      info: { main: scheme.info, contrastText: scheme.textOnInfo },
      background: { default: scheme.background, paper: scheme.surface },
      text: { primary: scheme.textPrimary, secondary: scheme.textSecondary, disabled: scheme.textDisabled },
      divider: scheme.lines,
      action: { active: scheme.textSecondary, hover: scheme.rowHover, disabled: scheme.textDisabled },
      chrome: { main: scheme.chrome, contrastText: scheme.textOnChrome, secondary: scheme.textOnChromeSecondary },
    },
    shape: { borderRadius: 6 },
    // A pick reaches the heading ranks only, as on the Blazor side: tables, fields and figures
    // keep the body's family.
    typography: { h1: heading, h2: heading, h3: heading, h4: heading, h5: heading, h6: heading },
    components: {
      // An outlined field's edge is a boundary, so it wears the ladder's input rung, not Lines.
      MuiOutlinedInput: {
        styleOverrides: { notchedOutline: { borderColor: scheme.linesInputs } },
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
        styleOverrides: { root: { textTransform: "none" } },
      },
      MuiToggleButton: {
        styleOverrides: { root: { textTransform: "none" } },
      },
    },
  });
}
