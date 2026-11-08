// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

import type { Field, Strings } from "@nsail/stack";

/** A wire value as a person reads it: the member's kind decides, the language formats. */
export function display(strings: Strings, field: Field, value: unknown): string {
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
    case "bigint":
      return BigInt(String(value)).toLocaleString(strings.language);
    case "decimal":
      // Intl formats a numeric string digit for digit; the ES2022 lib types only a number.
      return new Intl.NumberFormat(strings.language, { maximumFractionDigits: 20 }).format(String(value) as unknown as number);
    case "integer":
    case "number":
      return Number(value).toLocaleString(strings.language);
    default:
      return String(value);
  }
}
