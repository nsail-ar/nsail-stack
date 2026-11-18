// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

import { describe, expect, it } from "vitest";
import { isRetryable, requestFailed, type Problem } from "./problems";

function refused(status: number | null, code = "Refused"): Problem {
  return { code, title: code, issues: [], status };
}

describe("isRetryable", () => {
  it.each([null, 408, 429, 500, 502, 503, 504])("is true where a second try can pass (%s)", (status) => {
    expect(isRetryable(refused(status))).toBe(true);
  });

  it("is true for a request that got no response", () => {
    expect(isRetryable(requestFailed(null, null))).toBe(true);
  });

  it("is true for a problem that carries no status at all", () => {
    expect(isRetryable({ code: "Unreachable", title: "Unreachable", issues: [] })).toBe(true);
  });

  it.each([400, 401, 403, 404, 409, 410, 422])("is false for a refusal for good (%s)", (status) => {
    expect(isRetryable(refused(status))).toBe(false);
  });

  it("is false for the tenant's Suspended, though it is answered 503", () => {
    expect(isRetryable(refused(503, "Suspended"))).toBe(false);
  });
});
