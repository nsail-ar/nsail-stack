// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

import { useEffect, useState } from "react";
import type { Field } from "@nsail/messaging";
import { useStrings } from "./context";

export interface NsSearchProps {
  /** The message member the term is sent as: its key is the label, its bound the input's. */
  field: Field;
  value: string | null | undefined;
  onSearch: (term: string | null) => void;
  delay?: number;
}

/** A search box that sends once the typing stops, not on every key. */
export function NsSearch({ field, value, onSearch, delay = 300 }: NsSearchProps) {
  const strings = useStrings();
  const [term, setTerm] = useState(value ?? "");

  useEffect(() => {
    const trimmed = term.trim();

    if (trimmed === (value ?? "")) {
      return;
    }

    const timer = setTimeout(() => onSearch(trimmed.length > 0 ? trimmed : null), delay);

    return () => clearTimeout(timer);
  }, [term, value, onSearch, delay]);

  return (
    <input
      className="ns-search"
      type="search"
      aria-label={strings.translate(field.key)}
      placeholder={strings.translate("Common.Search")}
      maxLength={field.maxLength}
      value={term}
      onChange={(event) => setTerm(event.target.value)}
    />
  );
}
