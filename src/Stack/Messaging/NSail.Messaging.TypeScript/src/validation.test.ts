// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

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

  it("bounds a query member like a body one", () => {
    expect(validate(ListOrders, { search: "x".repeat(11) })?.issues[0].code).toBe("MaxLength");
  });
});
