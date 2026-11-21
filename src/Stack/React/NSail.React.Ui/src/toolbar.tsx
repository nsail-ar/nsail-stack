// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

import type { ReactNode } from "react";
import Stack from "@mui/material/Stack";

/** A row of controls (a search, a filter, a form's buttons) that wraps instead of overflowing. */
export function NsToolBar({ children }: { children: ReactNode }) {
  return (
    <Stack direction="row" spacing={1} useFlexGap sx={{ alignItems: "center", flexWrap: "wrap" }}>
      {children}
    </Stack>
  );
}
