// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack
// @vitest-environment happy-dom

import { act } from "react";
import { createRoot, type Root } from "react-dom/client";
import { afterEach, describe, expect, it } from "vitest";
import { defineMessage, Mediator, StackProvider, Strings } from "@nsail/stack";
import { NsSignedIn } from "./signed-in";

// act() refuses to drive a renderer unless the environment says it is a test one.
(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const GetSession = defineMessage<Record<never, never>, { isAuthenticated: boolean }>({
  name: "GetSession",
  key: "Iam.GetSession",
  area: "Iam",
  method: "GET",
  path: "api/iam/session",
  fields: {},
});

const mounted: Root[] = [];

afterEach(async () => {
  for (const root of mounted.splice(0)) {
    await act(async () => root.unmount());
  }
});

async function region(answer: Promise<Response>) {
  const host = document.createElement("div");
  const root = createRoot(host);
  const mediator = new Mediator({ fetch: (() => answer) as unknown as typeof fetch });

  mounted.push(root);

  await act(async () => {
    root.render(
      <StackProvider mediator={mediator} strings={new Strings("en", {})} session={GetSession}>
        <NsSignedIn anonymous={<span id="anonymous" />}>
          <span id="content" />
        </NsSignedIn>
      </StackProvider>,
    );
  });

  return host;
}

describe("NsSignedIn", () => {
  it("draws nothing while the session is being read", async () => {
    const host = await region(new Promise<Response>(() => {}));

    expect(host.querySelector("#content")).toBeNull();
    expect(host.querySelector("#anonymous")).toBeNull();
  });

  it("draws nothing of its content without a session", async () => {
    const host = await region(Promise.resolve(new Response('{"isAuthenticated":false}')));

    expect(host.querySelector("#content")).toBeNull();
    expect(host.querySelector("#anonymous")).not.toBeNull();
  });

  it("draws nothing of its content from a read that failed", async () => {
    const host = await region(Promise.resolve(new Response("", { status: 500 })));

    expect(host.querySelector("#content")).toBeNull();
  });

  it("draws its content once someone is signed in", async () => {
    const host = await region(Promise.resolve(new Response('{"isAuthenticated":true}')));

    expect(host.querySelector("#content")).not.toBeNull();
    expect(host.querySelector("#anonymous")).toBeNull();
  });
});
