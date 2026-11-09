// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack
// @vitest-environment happy-dom

import { act } from "react";
import { createRoot } from "react-dom/client";
import { afterEach, describe, expect, it, vi } from "vitest";
import { Mediator, StackProvider, Strings, stackStrings } from "@nsail/stack";
import { NsSearch } from "./search";

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const field = { kind: "string", key: "Shop.ListOrders.Term" } as const;

function draw(value: string | null, onSearch: (term: string | null) => void) {
  return (
    <StackProvider mediator={new Mediator()} strings={new Strings("en", stackStrings("en"))}>
      <NsSearch field={field} value={value} onSearch={onSearch} />
    </StackProvider>
  );
}

afterEach(() => {
  vi.useRealTimers();
});

describe("NsSearch", () => {
  it("clears its box and sends nothing when the parent clears the value", async () => {
    vi.useFakeTimers();

    const onSearch = vi.fn();
    const host = document.createElement("div");
    const root = createRoot(host);

    await act(async () => root.render(draw("abc", onSearch)));
    await act(async () => root.render(draw(null, onSearch)));
    await act(async () => {
      vi.advanceTimersByTime(1000);
    });

    expect(host.querySelector("input")!.value).toBe("");
    expect(onSearch).not.toHaveBeenCalled();

    await act(async () => root.unmount());
  });
});
