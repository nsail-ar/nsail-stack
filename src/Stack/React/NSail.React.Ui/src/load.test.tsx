// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack
// @vitest-environment happy-dom

import { act } from "react";
import { createRoot, type Root } from "react-dom/client";
import { afterEach, describe, expect, it } from "vitest";
import { Mediator, StackProvider, Strings, type Load, type Problem } from "@nsail/stack";
import { NsLoad } from "./load";

// act() refuses to drive a renderer unless the environment says it is a test one.
(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const mounted: Root[] = [];

afterEach(async () => {
  for (const root of mounted.splice(0)) {
    await act(async () => root.unmount());
  }
});

function problem(code: string, status: number | null): Problem {
  return { code, title: code, issues: [{ code, message: `${code} refused` }], status };
}

async function failed(refusal: Problem, reload: () => void = () => {}) {
  const host = document.createElement("div");
  const root = createRoot(host);
  const load: Load<string> = { state: "failed", data: undefined, problem: refusal, reload };

  mounted.push(root);

  await act(async () => {
    root.render(
      <StackProvider mediator={new Mediator()} strings={new Strings("en", {})}>
        <NsLoad load={load}>{(data) => <span id="loaded">{data}</span>}</NsLoad>
      </StackProvider>,
    );
  });

  return host;
}

describe("NsLoad", () => {
  // A second try can never turn these around, so a Retry would only repeat the refusal.
  it.each([
    ["Forbidden", 403],
    ["Unauthorized", 401],
    ["FeatureUnavailable", 403],
    ["NotFound", 404],
    ["Conflict", 409],
    ["RuleViolation", 422],
    ["InvalidModel", 400],
    ["Suspended", 503],
  ])("draws a %s refusal (%i) and no retry", async (code, status) => {
    const host = await failed(problem(code, status));

    expect(host.querySelector(".ns-failure")?.textContent).toContain(`${code} refused`);
    expect(host.querySelector("button")).toBeNull();
    expect(host.querySelector("#loaded")).toBeNull();
  });

  it.each([null, 408, 429, 500, 503, 504])("offers the retry where a second try can pass (%s)", async (status) => {
    let reloads = 0;
    const host = await failed(problem("RequestFailed", status), () => reloads++);

    const retry = host.querySelector<HTMLButtonElement>(".ns-failure button");
    expect(retry).not.toBeNull();

    await act(async () => retry!.click());

    expect(reloads).toBe(1);
  });
});
