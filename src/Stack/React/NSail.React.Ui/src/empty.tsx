// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

import type { ReactNode } from "react";
import Stack from "@mui/material/Stack";
import Typography from "@mui/material/Typography";
import { useStrings } from "@nsail/stack";

export interface NsEmptyProps {
  /** A string key: what the empty region says. */
  text: string;
  /** A glyph above the sentence. */
  icon?: ReactNode;
  /** The act that fills the region, such as a primary NsButton. */
  action?: ReactNode;
}

/** A region with nothing in it, said rather than left blank. Only for an answer that arrived
 * empty: a read that failed is NsLoad's failure, never this. */
export function NsEmpty({ text, icon, action }: NsEmptyProps) {
  const strings = useStrings();

  return (
    <Stack role="status" spacing={1.5} sx={{ alignItems: "center", textAlign: "center", py: 5, color: "text.secondary" }}>
      {icon}
      <Typography>{strings.translate(text)}</Typography>
      {action}
    </Stack>
  );
}
