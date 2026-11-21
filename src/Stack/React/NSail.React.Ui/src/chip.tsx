// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

import Chip from "@mui/material/Chip";
import { useStrings } from "@nsail/stack";

export type NsSeverity = "info" | "success" | "warning" | "error";

export interface NsChipProps {
  /** A string key: the state the chip names. */
  text: string;
  /** The state's tone; none is the neutral chip a plain value wears. */
  severity?: NsSeverity;
  /** With a severity, the filled face, for a state that has to read from across the screen. */
  solid?: boolean;
}

/** A state, named: its word carries the meaning and the tone only repeats it. */
export function NsChip({ text, severity, solid = false }: NsChipProps) {
  const strings = useStrings();

  return <Chip size="small" label={strings.translate(text)} color={severity ?? "default"} variant={severity && solid ? "filled" : "outlined"} />;
}
