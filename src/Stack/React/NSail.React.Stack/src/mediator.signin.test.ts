// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

import { describe, expect, it } from "vitest";
import { Mediator } from "./mediator";
import { BusinessError } from "./problems";
import { ListOrders } from "./fixtures.test-support";

const denied = { code: "Unauthorized", title: "Sign in required", issues: [{ code: "Unauthorized", message: "x" }], status: 401 };

function answering(status: number, body: unknown, options: { signIn?: string; pathname?: string; search?: string } = {}) {
  const calls: RequestInit[] = [];
  const turns: string[] = [];
  const fetcher = (async (_: string, init: RequestInit) => {
    calls.push(init);

    return new Response(JSON.stringify(body), { status });
  }) as unknown as typeof fetch;
  const location = { pathname: options.pathname ?? "/contacts", search: options.search ?? "?page=2", hash: "", assign: (url: string) => turns.push(url) };

  return { calls, turns, mediator: new Mediator({ fetch: fetcher, signIn: options.signIn, location: location as unknown as Location }) };
}

describe("Mediator's turn to sign-in", () => {
  it("turns a 401 to the declared address carrying where the visitor was", async () => {
    const { turns, mediator } = answering(401, denied, { signIn: "/iam/sign-in" });

    await expect(mediator.send(ListOrders, {})).rejects.toBeInstanceOf(BusinessError);

    expect(turns).toEqual(["/iam/sign-in?returnUrl=%2Fcontacts%3Fpage%3D2"]);
  });

  it("turns once however many sends answer 401 together", async () => {
    const { turns, mediator } = answering(401, denied, { signIn: "/iam/sign-in" });

    await Promise.allSettled([mediator.send(ListOrders, {}), mediator.send(ListOrders, {}), mediator.send(ListOrders, {})]);

    expect(turns).toHaveLength(1);
  });

  it.each([
    ["/", ""],
    ["/iam/sign-in", ""],
    ["/IAM/Sign-In/", "?x=1"],
  ])("carries no return address from %s%s", async (pathname, search) => {
    const { turns, mediator } = answering(401, denied, { signIn: "/iam/sign-in", pathname, search });

    await mediator.send(ListOrders, {}).catch(() => {});

    expect(turns).toEqual(["/iam/sign-in"]);
  });

  it("joins the return address to a sign-in address that has its own query", async () => {
    const { turns, mediator } = answering(401, denied, { signIn: "/iam/sign-in?mode=web" });

    await mediator.send(ListOrders, {}).catch(() => {});

    expect(turns).toEqual(["/iam/sign-in?mode=web&returnUrl=%2Fcontacts%3Fpage%3D2"]);
  });

  it("says the credential was refused when the 401 met a signed-in session", async () => {
    const { turns, mediator } = answering(401, denied, { signIn: "/iam/sign-in" });

    mediator.reportSession(true);
    await mediator.send(ListOrders, {}).catch(() => {});

    expect(turns).toEqual(["/iam/sign-in?returnUrl=%2Fcontacts%3Fpage%3D2&staleCredential=1"]);
  });

  it("says nothing of a refused credential once the client reported a sign-out", async () => {
    const { turns, mediator } = answering(401, denied, { signIn: "/iam/sign-in", pathname: "/", search: "" });

    mediator.reportSession(true);
    mediator.reportSession(false);
    await mediator.send(ListOrders, {}).catch(() => {});

    expect(turns).toEqual(["/iam/sign-in"]);
  });

  it("never turns a 403: the refusal stays where the send was made", async () => {
    const forbidden = { code: "Forbidden", title: "Not allowed", issues: [{ code: "Forbidden", message: "x" }], status: 403 };
    const { turns, mediator } = answering(403, forbidden, { signIn: "/iam/sign-in" });
    const error = await mediator.send(ListOrders, {}).catch((e: unknown) => e);

    expect((error as BusinessError).problem).toEqual(forbidden);
    expect(turns).toHaveLength(0);
  });

  it("meets a 401 as a refusal carrying the server's Problem when no address is declared", async () => {
    const { turns, mediator } = answering(401, denied);
    const error = await mediator.send(ListOrders, {}).catch((e: unknown) => e);

    expect(error).toBeInstanceOf(BusinessError);
    expect((error as BusinessError).problem).toEqual(denied);
    expect(turns).toHaveLength(0);
  });

  it("sends the page's own cookie and mints no credential of its own", async () => {
    const { calls, mediator } = answering(200, { items: [] }, { signIn: "/iam/sign-in" });

    await mediator.send(ListOrders, {});

    expect(calls[0].credentials).toBe("same-origin");
    expect(Object.keys(calls[0].headers as Record<string, string>).map((name) => name.toLowerCase())).not.toContain("authorization");
  });
});
