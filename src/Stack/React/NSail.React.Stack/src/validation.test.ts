// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

import { describe, expect, it } from "vitest";
import { validate, validateField } from "./validation";
import { ListOrders, UpdateOrder } from "./fixtures.test-support";

describe("validate", () => {
  it("speaks MessageValidator's codes, sources and arguments", () => {
    const problem = validate(UpdateOrder, { id: "1", number: "TOO-LONG", email: "nope", quantity: 120 });

    expect(problem?.code).toBe("InvalidModel");
    expect(problem?.issues).toEqual([
      { code: "MaxLength", message: "The field Number exceeds the maximum length of 5", source: "Number", arguments: { field: "Number", max: "5" } },
      { code: "InvalidFormat", message: "The field Email has an invalid format", source: "Email", arguments: { field: "Email" } },
      { code: "OutOfRange", message: "The field Quantity must be between 1 and 99", source: "Quantity", arguments: { field: "Quantity", from: "1", to: "99" } },
    ]);
  });

  it("leaves an absent value to Required alone", () => {
    expect(validate(UpdateOrder, { id: "1", number: "A", email: null })).toBeNull();
    expect(validateField(UpdateOrder.fields.number, "   ").map((i) => i.code)).toEqual(["Required"]);
  });

  it("bounds an exact number carried as text", () => {
    const price = { kind: "decimal" as const, key: "Shop.UpdateOrder.Price", min: 0, max: 10 };

    expect(validateField(price, "9.999999999999999999").map((i) => i.code)).toEqual([]);
    expect(validateField(price, "10.5").map((i) => i.code)).toEqual(["OutOfRange"]);
    expect(validateField({ ...price, kind: "bigint" as const }, "-1").map((i) => i.code)).toEqual(["OutOfRange"]);
  });

  // The text input takes anything; what the server's parse would refuse is refused here first.
  it("refuses a long or decimal text the server cannot read", () => {
    const price = { kind: "decimal" as const, key: "Shop.UpdateOrder.Price" };
    const id = { kind: "bigint" as const, key: "Shop.UpdateOrder.Id" };

    expect(validateField(price, "1234567890.123456789").map((i) => i.code)).toEqual([]);
    expect(validateField(price, "-1.5e3").map((i) => i.code)).toEqual([]);
    expect(validateField(price, "1234,56").map((i) => i.code)).toEqual(["InvalidFormat"]);
    expect(validateField(price, "abc").map((i) => i.code)).toEqual(["InvalidFormat"]);
    expect(validateField(id, "9007199254740993").map((i) => i.code)).toEqual([]);
    expect(validateField(id, "1.5").map((i) => i.code)).toEqual(["InvalidFormat"]);
  });

  it("bounds a query member like a body one", () => {
    expect(validate(ListOrders, { search: "x".repeat(11) })?.issues[0].code).toBe("MaxLength");
  });
});
