// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack
// @vitest-environment happy-dom

import { act } from "react";
import { createRoot } from "react-dom/client";
import { describe, expect, it } from "vitest";
import { StackProvider } from "./context";
import { useLoad, type Load } from "./load";
import { Mediator } from "./mediator";
import { Strings } from "./strings";
import { ListOrders } from "./fixtures.test-support";

// act() refuses to drive a renderer unless the environment says it is a test one.
(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

describe("useLoad", () => {
  it("drops the answer of a superseded request even when fetch ignores the signal", async () => {
    const pending: { search: string | null | undefined; resolve: (body: string) => void }[] = [];
    const fetcher = ((url: string) =>
      new Promise<Response>((resolve) => {
        pending.push({ search: new URL(url).searchParams.get("search"), resolve: (body) => resolve(new Response(body)) });
      })) as unknown as typeof fetch;
    const mediator = new Mediator({ fetch: fetcher, baseUrl: "https://host" });
    let current: Load<{ items: unknown[] }> | undefined;

    function Probe({ search }: { search: string }) {
      current = useLoad(ListOrders, { search });

      return null;
    }

    const root = createRoot(document.createElement("div"));
    const render = (search: string) =>
      act(async () => {
        root.render(
          <StackProvider mediator={mediator} strings={new Strings("en", {})}>
            <Probe search={search} />
          </StackProvider>,
        );
      });

    await render("a");
    await render("b");
    expect(pending.map((call) => call.search)).toEqual(["a", "b"]);

    await act(async () => pending[1].resolve('{"items":["new"]}'));
    await act(async () => pending[0].resolve('{"items":["old"]}'));

    expect(current?.state).toBe("loaded");
    expect(current?.data).toEqual({ items: ["new"] });

    await act(async () => root.unmount());
  });
});
