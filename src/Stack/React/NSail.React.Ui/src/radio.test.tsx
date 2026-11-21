// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack
// @vitest-environment happy-dom


import { act } from "react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { defineEnum, type Field } from "@nsail/stack";
import { NsRadioGroup } from "./radio";
import { clear, draw } from "./app.test-support";

afterEach(clear);

const Period = defineEnum("Shop.Period", ["Monthly", "Yearly"]);
const field: Field = { kind: "enum", key: "Shop.ChangePlan.Period", enum: Period };
const catalog = { "Shop.ChangePlan.Period": "Facturación", "Shop.Period.Monthly": "Mensual", "Shop.Period.Yearly": "Anual" };

describe("NsRadioGroup", () => {
  it("names the group by its member and each face by its value", async () => {
    const { host } = await draw(<NsRadioGroup field={field} value="Yearly" onChange={() => {}} />, { catalog });
    const group = host.querySelector("[role=group]")!;

    expect(group.getAttribute("aria-label")).toBe("Facturación");
    expect([...group.querySelectorAll("button")].map((face) => [face.textContent, face.getAttribute("aria-pressed")])).toEqual([
      ["Mensual", "false"],
      ["Anual", "true"],
    ]);
  });

  it("answers the face pressed, and never an empty answer", async () => {
    const onChange = vi.fn();
    const { host } = await draw(<NsRadioGroup field={field} value="Yearly" onChange={onChange} />, { catalog });
    const [monthly, yearly] = host.querySelectorAll("button");

    await act(async () => yearly.click());
    await act(async () => monthly.click());

    expect(onChange.mock.calls).toEqual([["Monthly"]]);
  });
});
