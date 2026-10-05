// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

import type { ButtonHTMLAttributes } from "react";
import { useStrings } from "@nsail/stack";

export interface NsButtonProps extends Omit<ButtonHTMLAttributes<HTMLButtonElement>, "children"> {
  /** A string key, never a sentence: the label is the catalog's. */
  text: string;
  variant?: "default" | "primary" | "danger" | "quiet";
  busy?: boolean;
}

export function NsButton({ text, variant = "default", busy = false, type = "button", disabled, className, ...rest }: NsButtonProps) {
  const strings = useStrings();

  return (
    <button {...rest} type={type} disabled={disabled || busy} aria-busy={busy || undefined} className={`ns-button ns-button-${variant}${className ? ` ${className}` : ""}`}>
      {strings.translate(text)}
    </button>
  );
}
