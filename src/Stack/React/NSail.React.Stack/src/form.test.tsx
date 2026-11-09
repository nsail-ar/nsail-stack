// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack
// @vitest-environment happy-dom

import { act } from "react";
import { createRoot, type Root } from "react-dom/client";
import { afterEach, describe, expect, it } from "vitest";
import { useForm, type Form } from "./form";
import { BusinessError } from "./problems";
import { UpdateOrder } from "./fixtures.test-support";

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const initial: UpdateOrder = { id: "0b0e6a3e-6d1c-4a0c-9d57-1f1f2f6a9a01", number: "A1", quantity: 1 };
const mounted: Root[] = [];

function hold(): { current: Form<UpdateOrder> } {
  const form = {} as { current: Form<UpdateOrder> };

  function Probe() {
    form.current = useForm(UpdateOrder, initial);

    return null;
  }

  const root = createRoot(document.createElement("div"));
  mounted.push(root);
  act(() => root.render(<Probe />));

  return form;
}

function pending() {
  let resolve!: () => void;
  let reject!: (error: unknown) => void;
  const promise = new Promise<void>((res, rej) => {
    resolve = res;
    reject = rej;
  });

  return { promise, resolve, reject };
}

afterEach(() => {
  mounted.splice(0).forEach((root) => act(() => root.unmount()));
});

describe("useForm.submit", () => {
  it("sends once when called again while a send is in flight", async () => {
    const form = hold();
    const send = pending();
    let calls = 0;
    const action = () => {
      calls++;

      return send.promise;
    };
    let first!: Promise<boolean>;
    let second!: boolean;

    act(() => {
      first = form.current.submit(action);
    });
    await act(async () => {
      second = await form.current.submit(action);
    });

    expect(second).toBe(false);
    expect(calls).toBe(1);
    expect(form.current.submitting).toBe(true);

    await act(async () => {
      send.resolve();
      await first;
    });

    expect(form.current.submitting).toBe(false);
    expect(form.current.saved).toBe(true);
  });

  it("sends again once the first send has ended", async () => {
    const form = hold();
    let calls = 0;
    const action = async () => {
      calls++;
    };

    await act(async () => {
      await form.current.submit(action);
    });
    await act(async () => {
      await form.current.submit(action);
    });

    expect(calls).toBe(2);
  });

  it("stays unsaved when a field changed during the send", async () => {
    const form = hold();
    const send = pending();
    let landed!: Promise<boolean>;

    act(() => {
      landed = form.current.submit(() => send.promise);
    });
    act(() => form.current.set("number", "B2"));
    await act(async () => {
      send.resolve();

      expect(await landed).toBe(true);
    });

    expect(form.current.saved).toBe(false);
    expect(form.current.submitting).toBe(false);
  });

  it("sets submitting, ends saved and shows the server's issues on their fields", async () => {
    const form = hold();
    const send = pending();
    let landed!: Promise<boolean>;

    act(() => {
      landed = form.current.submit(() => send.promise);
    });

    expect(form.current.submitting).toBe(true);

    await act(async () => {
      send.resolve();
      await landed;
    });

    expect(form.current.saved).toBe(true);

    const refusal = pending();
    let refused!: Promise<boolean>;

    act(() => {
      refused = form.current.submit(() => refusal.promise);
    });
    await act(async () => {
      refusal.reject(
        new BusinessError({ title: "Invalid", code: "InvalidModel", issues: [{ code: "TooLong", source: "Number", message: "x" }] }),
      );

      expect(await refused).toBe(false);
    });

    expect(form.current.saved).toBe(false);
    expect(form.current.issues("number").map((i) => i.code)).toContain("TooLong");
  });

  it("clears saved on reset", async () => {
    const form = hold();

    await act(async () => {
      await form.current.submit(async () => {});
    });

    expect(form.current.saved).toBe(true);

    act(() => form.current.reset(initial));

    expect(form.current.saved).toBe(false);
  });
});
