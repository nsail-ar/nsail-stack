// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

import type { ButtonHTMLAttributes } from "react";
import Button from "@mui/material/Button";
import { useStrings } from "@nsail/stack";

export interface NsButtonProps extends Omit<ButtonHTMLAttributes<HTMLButtonElement>, "children" | "color"> {
  /** A string key, never a sentence: the label is the catalog's. */
  text: string;
  variant?: "default" | "primary" | "danger" | "quiet";
  busy?: boolean;
}

const faces = {
  default: { variant: "outlined", color: "inherit" },
  primary: { variant: "contained", color: "primary" },
  danger: { variant: "contained", color: "error" },
  quiet: { variant: "text", color: "primary" },
} as const;

export function NsButton({ text, variant = "default", busy = false, type = "button", disabled, ...rest }: NsButtonProps) {
  const strings = useStrings();

  return (
    <Button {...rest} {...faces[variant]} type={type} disabled={disabled || busy} loading={busy || undefined} aria-busy={busy || undefined}>
      {strings.translate(text)}
    </Button>
  );
}
