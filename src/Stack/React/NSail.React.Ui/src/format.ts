// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

import type { Field, Strings } from "@nsail/stack";

const whole = /^-?\d+$/;
const fixed = /^-?\d*(\.\d+)?$/;

export interface DisplayOptions {
  /** False renders a number without digit grouping. */
  grouped?: boolean;
}

/** A wire value as a person reads it: the member's kind decides, the language formats. */
export function display(strings: Strings, field: Field, value: unknown, options?: DisplayOptions): string {
  const useGrouping = options?.grouped !== false;

  if (value === null || value === undefined || value === "") {
    return "";
  }

  switch (field.kind) {
    case "enum":
      return field.enum ? strings.enumValue(field.enum, String(value)) : String(value);
    case "boolean":
      return strings.translate(value ? "Common.Yes" : "Common.No");
    case "date": {
      // A business date carries no zone; read as UTC midnight it cannot slide a day.
      const date = new Date(`${String(value)}T00:00:00Z`);

      return date.toLocaleDateString(strings.language, { timeZone: "UTC" });
    }
    case "datetime":
      return new Date(String(value)).toLocaleString(strings.language, { dateStyle: "short", timeStyle: "short" });
    // A value that is not the wire's text (a Number a product still writes) shows as a figure.
    case "bigint":
      return whole.test(String(value)) ? BigInt(String(value)).toLocaleString(strings.language, { useGrouping }) : Number(value).toLocaleString(strings.language, { useGrouping });
    case "decimal":
      return fixed.test(String(value)) ? decimal(strings.language, String(value), useGrouping) : Number(value).toLocaleString(strings.language, { useGrouping });
    case "integer":
    case "number":
      return Number(value).toLocaleString(strings.language, { useGrouping });
    default:
      return String(value);
  }
}

// A decimal carries up to 28 fraction digits and Intl stops at 20 (and reads a string as a
// Number before Intl V3): the whole part is grouped as a BigInt and the fraction kept as sent.
function decimal(language: string, text: string, useGrouping: boolean): string {
  const negative = text.startsWith("-");
  const [whole, fraction] = (negative ? text.slice(1) : text).split(".");
  const parts = new Intl.NumberFormat(language).formatToParts(-0.5);
  const point = parts.find((part) => part.type === "decimal")?.value ?? ".";
  const minus = parts.find((part) => part.type === "minusSign")?.value ?? "-";
  const grouped = BigInt(whole === "" ? "0" : whole).toLocaleString(language, { useGrouping });

  return `${negative ? minus : ""}${grouped}${fraction ? `${point}${fraction}` : ""}`;
}
