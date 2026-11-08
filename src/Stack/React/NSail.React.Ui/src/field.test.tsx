// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack
// @vitest-environment happy-dom

import { act } from "react";
import { createRoot, type Root } from "react-dom/client";
import { afterEach, describe, expect, it } from "vitest";
import { defineMessage, Mediator, StackProvider, Strings, stackStrings, useForm } from "@nsail/stack";
import { NsField } from "./field";
import { NsForm } from "./form";

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

interface Reprice {
  price?: string;
  floor?: string | null;
}

const Reprice = defineMessage<Reprice, Reprice>({
  name: "Reprice",
  key: "Shop.Reprice",
  area: "Shop",
  method: "POST",
  path: "api/shop/reprice",
  fields: {
    price: { kind: "decimal", key: "Shop.Reprice.Price", binding: "body" },
    floor: { kind: "decimal", key: "Shop.Reprice.Floor", binding: "body", nullable: true },
  },
});

const mounted: Root[] = [];

afterEach(() => {
  act(() => mounted.splice(0).forEach((root) => root.unmount()));
  document.body.innerHTML = "";
});

function render(): { value: () => Reprice; type: (name: keyof Reprice, text: string) => void } {
  let current: Reprice = {};

  function Screen() {
    const form = useForm(Reprice, {});

    current = form.value;

    return (
      <NsForm form={form} onSubmit={async () => {}}>
        <NsField<Reprice> name="price" />
        <NsField<Reprice> name="floor" />
      </NsForm>
    );
  }

  const host = document.body.appendChild(document.createElement("div"));
  const root = createRoot(host);

  mounted.push(root);
  act(() => {
    root.render(
      <StackProvider mediator={new Mediator()} strings={new Strings("en", stackStrings("en"))}>
        <Screen />
      </StackProvider>,
    );
  });

  return {
    value: () => current,
    type: (name, text) => {
      const input = host.querySelector<HTMLInputElement>(`input[name="${name}"]`)!;
      const setter = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, "value")!.set!;

      act(() => {
        setter.call(input, text);
        input.dispatchEvent(new Event("input", { bubbles: true }));
      });
    },
  };
}

// An emptied exact number is omitted, so the message's own default stands: "" is no number the
// server reads.
describe("NsField", () => {
  it("keeps an exact number as its text and leaves an emptied one absent", () => {
    const screen = render();

    screen.type("price", "1234567890.123456789");
    screen.type("floor", "5");
    expect(screen.value()).toEqual({ price: "1234567890.123456789", floor: "5" });

    screen.type("price", "");
    screen.type("floor", "");
    expect(screen.value()).toEqual({ price: undefined, floor: null });
  });
});
