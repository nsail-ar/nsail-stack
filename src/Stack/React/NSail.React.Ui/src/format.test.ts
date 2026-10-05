// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

import { describe, expect, it } from "vitest";
import { defineEnum, Strings, stackStrings, type Field } from "@nsail/stack";
import { display } from "./format";

const strings = new Strings("es", { ...stackStrings("es"), "Shop.Status.Open": "Abierto" });
const Status = defineEnum("Shop.Status", ["Open", "Shipped"]);

function field(kind: Field["kind"], extra: Partial<Field> = {}): Field {
  return { kind, key: "Shop.OrderRow.X", ...extra };
}

describe("display", () => {
  it("speaks an enum value through its key, its name where the catalog has none", () => {
    expect(display(strings, field("enum", { enum: Status }), "Open")).toBe("Abierto");
    expect(display(strings, field("enum", { enum: Status }), "Shipped")).toBe("Shipped");
  });

  it("reads a boolean with the catalog's own yes and no", () => {
    expect(display(strings, field("boolean"), true)).toBe("Sí");
    expect(display(strings, field("boolean"), false)).toBe("No");
  });

  it("never slides a business date across midnight", () => {
    expect(display(strings, field("date"), "2026-01-01")).toBe(new Date(Date.UTC(2026, 0, 1)).toLocaleDateString("es", { timeZone: "UTC" }));
  });

  it("renders absent as nothing", () => {
    expect(display(strings, field("string"), null)).toBe("");
    expect(display(strings, field("integer"), undefined)).toBe("");
  });
});
