// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

import { describe, expect, it } from "vitest";
import { Mediator } from "./mediator";
import { BusinessError } from "./problems";
import { ListOrders, UpdateOrder } from "./fixtures.test-support";

function recording(status = 200, body = "") {
  const calls: { url: string; init: RequestInit }[] = [];
  const fetcher = (async (url: string, init: RequestInit) => {
    calls.push({ url, init });

    return new Response(body.length ? body : null, { status });
  }) as unknown as typeof fetch;

  return { calls, mediator: new Mediator({ fetch: fetcher, baseUrl: "https://host/" }) };
}

describe("Mediator.send", () => {
  it("puts route tokens in the path, query members in the query and leaves an unset filter out", async () => {
    const { calls, mediator } = recording(200, '{"items":[]}');

    await mediator.send(ListOrders, { search: "ana", status: null, ids: ["a", "b"], pageIndex: 2 });

    expect(calls[0].url).toBe("https://host/api/shop/orders?search=ana&ids=a&ids=b&pageIndex=2");
    expect(calls[0].init.method).toBe("GET");
    expect(calls[0].init.body).toBeUndefined();
  });

  it("sends body members as JSON, header members as headers, and the route member in the path only", async () => {
    const { calls, mediator } = recording(200);

    await mediator.send(UpdateOrder, { id: "6f9c", number: "A-1", quantity: 3, token: "t-1" });

    expect(calls[0].url).toBe("https://host/api/shop/orders/6f9c");
    expect(JSON.parse(String(calls[0].init.body))).toEqual({ number: "A-1", quantity: 3 });
    expect((calls[0].init.headers as Record<string, string>).token).toBe("t-1");
  });

  it("answers a void message with nothing", async () => {
    const { mediator } = recording(200);

    await expect(mediator.send(UpdateOrder, { id: "1", number: "A" })).resolves.toBeUndefined();
  });

  it("refuses an invalid request before anything leaves", async () => {
    const { calls, mediator } = recording(200);
    const error = await mediator.send(UpdateOrder, { id: "1", number: "", quantity: 0 }).catch((e: unknown) => e);

    expect(error).toBeInstanceOf(BusinessError);
    expect((error as BusinessError).problem.issues.map((i) => [i.code, i.source])).toEqual([
      ["Required", "Number"],
      ["OutOfRange", "Quantity"],
    ]);
    expect(calls).toHaveLength(0);
  });

  it("turns the server's Problem into a BusinessError carrying it", async () => {
    const problem = { code: "NotFound", title: "Resource not found", issues: [{ code: "NotFound", message: "x", source: "Order" }], status: 404 };
    const { mediator } = recording(404, JSON.stringify(problem));
    const error = await mediator.send(UpdateOrder, { id: "1", number: "A" }).catch((e: unknown) => e);

    expect((error as BusinessError).problem).toEqual(problem);
  });

  it("names a failure with no Problem the way NetworkProblem does", async () => {
    const { mediator } = recording(502, "<html>bad gateway</html>");
    const error = await mediator.send(UpdateOrder, { id: "1", number: "A" }).catch((e: unknown) => e);

    expect((error as BusinessError).problem.code).toBe("RequestFailed");
    expect((error as BusinessError).problem.issues.map((i) => i.code)).toEqual(["HttpStatus", "DeserializationFailed"]);
  });
});
