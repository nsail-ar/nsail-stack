// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

import type { ReactNode } from "react";
import Alert from "@mui/material/Alert";
import { useStrings } from "@nsail/stack";
import type { NsSeverity } from "./chip";

export interface NsAlertProps {
  /** A string key: what the alert says. */
  text: string;
  /** Named arguments of the sentence (Strings.format). */
  args?: Record<string, string>;
  severity?: NsSeverity;
  /** An act beside the sentence, such as a quiet NsButton. */
  action?: ReactNode;
}

/** A message that sits on the screen. An error or a warning is read out as it arrives, since it
 * has to be heard before the person acts; the other tones are copy, not news. */
export function NsAlert({ text, args, severity = "info", action }: NsAlertProps) {
  const strings = useStrings();
  const urgent = severity === "error" || severity === "warning";

  return (
    <Alert severity={severity} role={urgent ? "alert" : "note"} action={action}>
      {args ? strings.format(text, args) : strings.translate(text)}
    </Alert>
  );
}
