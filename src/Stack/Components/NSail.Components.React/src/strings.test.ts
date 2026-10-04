// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

import { describe, expect, it } from "vitest";
import { defineEnum } from "@nsail/messaging";
import { Strings, stackStrings } from "./strings";

const strings = new Strings("es", {
  ...stackStrings("es"),
  "Shop.Order": "Pedido",
  "Problems.Required.Number": "Poné el número",
  "Shop.Status.Open": "Abierto",
});

describe("Strings", () => {
  it("resolves an issue most specific first, then by code, then by the sender's message", () => {
    expect(strings.issue({ code: "Required", message: "x", source: "Number" })).toBe("Poné el número");
    expect(strings.issue({ code: "Required", message: "x", source: "Name" })).toBe("Obligatorio");
    expect(strings.issue({ code: "Unheard", message: "As sent" })).toBe("As sent");
  });

  it("fills named tokens and resolves the entity argument as a key", () => {
    expect(strings.issue({ code: "MaxLength", message: "x", arguments: { max: "5" } })).toBe("No más de 5 caracteres");
    expect(strings.issue({ code: "NotFound", message: "x", arguments: { entity: "Shop.Order" } })).toBe("No se encontró Pedido");
    expect(strings.issue({ code: "NotFound", message: "x", arguments: { entity: "Shop.Invoice" } })).toBe("No se encontró Invoice");
  });

  it("renders a missing key as itself and an enum value by its key", () => {
    expect(strings.translate("Shop.Nowhere")).toBe("Shop.Nowhere");
    expect(strings.enumValue(defineEnum("Shop.Status", ["Open", "Shipped"]), "Open")).toBe("Abierto");
    expect(strings.enumValue(defineEnum("Shop.Status", ["Open", "Shipped"]), "Shipped")).toBe("Shipped");
  });

  it("reads the Blazor catalog for the Stack's own sentences", () => {
    expect(strings.translate("Common.Submit")).toBe("Guardar");
    expect(strings.problem({ code: "Conflict", title: "x", issues: [] })).toBe("Conflicto de concurrencia");
    expect(strings.format("Mud.MudDataGridPager_InfoFormat", ["1", "10", "42"])).toBe("1-10 de 42");
  });
});
