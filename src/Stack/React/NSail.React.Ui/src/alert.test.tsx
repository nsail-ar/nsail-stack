// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack
// @vitest-environment happy-dom


import { afterEach, describe, expect, it } from "vitest";
import { NsAlert } from "./alert";
import { clear, draw } from "./app.test-support";

afterEach(clear);

const catalog = { "Shop.Plan.Ends": "Tu plan vence el {date}", "Shop.Plan.Expired": "Tu plan venció" };

describe("NsAlert", () => {
  it("says its key's sentence with the arguments filled", async () => {
    const { host } = await draw(<NsAlert text="Shop.Plan.Ends" args={{ date: "1/12" }} />, { catalog });

    expect(host.querySelector("[role=note]")!.textContent).toBe("Tu plan vence el 1/12");
  });

  // Read out as it arrives: the person has to hear it before acting.
  it.each(["error", "warning"] as const)("is read out when it is a %s", async (severity) => {
    const { host } = await draw(<NsAlert text="Shop.Plan.Expired" severity={severity} />, { catalog });

    expect(host.querySelector("[role=alert]")!.textContent).toBe("Tu plan venció");
  });

  it.each(["info", "success"] as const)("sits as copy when it is %s", async (severity) => {
    const { host } = await draw(<NsAlert text="Shop.Plan.Expired" severity={severity} />, { catalog });

    expect(host.querySelector("[role=alert]")).toBeNull();
  });
});
