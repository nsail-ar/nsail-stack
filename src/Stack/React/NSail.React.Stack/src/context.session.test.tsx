// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack
// @vitest-environment happy-dom

import { act } from "react";
import { createRoot } from "react-dom/client";
import { describe, expect, it } from "vitest";
import { StackProvider, useSession } from "./context";
import type { Load } from "./load";
import { Mediator } from "./mediator";
import { Strings } from "./strings";
import { GetSession, ListOrders, type Session } from "./fixtures.test-support";

// act() refuses to drive a renderer unless the environment says it is a test one.
(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

describe("useSession", () => {
  it("answers who is signed in and re-reads after a sign-in and a sign-out the client reports", async () => {
    const answers: Session[] = [
      { isAuthenticated: false },
      { isAuthenticated: true, displayName: "Ana" },
      { isAuthenticated: false },
    ];
    const urls: string[] = [];
    const fetcher = (async (url: string) => {
      urls.push(url);

      return new Response(JSON.stringify(answers[urls.length - 1]));
    }) as unknown as typeof fetch;
    const mediator = new Mediator({ fetch: fetcher });
    const seen: Load<Session>[] = [];

    function Probe() {
      seen.push(useSession<Session>());

      return null;
    }

    const root = createRoot(document.createElement("div"));

    await act(async () => {
      root.render(
        <StackProvider mediator={mediator} strings={new Strings("en", {})} session={GetSession}>
          <Probe />
        </StackProvider>,
      );
    });

    expect(urls).toEqual(["/api/iam/session"]);
    expect(seen[0].state).toBe("loading");
    expect(seen.at(-1)?.data).toEqual({ isAuthenticated: false });

    await act(async () => seen.at(-1)!.reload());
    expect(seen.at(-1)?.data).toEqual({ isAuthenticated: true, displayName: "Ana" });

    await act(async () => seen.at(-1)!.reload());
    expect(seen.at(-1)?.data).toEqual({ isAuthenticated: false });
    expect(urls).toHaveLength(3);

    await act(async () => root.unmount());
  });

  it("reports a signed-in session to the mediator, so a later 401 turns marked stale", async () => {
    const turns: string[] = [];
    const fetcher = (async (url: string) => {
      return url.endsWith("/session") ? new Response(JSON.stringify({ isAuthenticated: true })) : new Response("{}", { status: 401 });
    }) as unknown as typeof fetch;
    const location = { pathname: "/", search: "", hash: "", assign: (url: string) => turns.push(url) };
    const mediator = new Mediator({ fetch: fetcher, signIn: "/iam/sign-in", location: location as unknown as Location });
    const root = createRoot(document.createElement("div"));

    await act(async () => {
      root.render(
        <StackProvider mediator={mediator} strings={new Strings("en", {})} session={GetSession}>
          {null}
        </StackProvider>,
      );
    });

    await mediator.send(ListOrders, {}).catch(() => {});

    expect(turns).toEqual(["/iam/sign-in?staleCredential=1"]);

    await act(async () => root.unmount());
  });

  it("refuses a reader with no session message declared", () => {
    function Probe() {
      useSession();

      return null;
    }

    const root = createRoot(document.createElement("div"));

    expect(() =>
      act(() => {
        root.render(
          <StackProvider mediator={new Mediator()} strings={new Strings("en", {})}>
            <Probe />
          </StackProvider>,
        );
      }),
    ).toThrow(/session/);
  });
});
