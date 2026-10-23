// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack
// @vitest-environment happy-dom

import { act, useEffect, type ReactNode } from "react";
import { createRoot, type Root } from "react-dom/client";
import { afterEach, describe, expect, it } from "vitest";
import { Mediator, StackProvider, Strings, stackStrings } from "@nsail/stack";
import { NsDialogHost, useConfirm } from "./dialog";

// A confirmation has nothing to type into, so what the cursor opens on is what a bare Enter
// presses (ui/forms.md, Focus on open): an ordinary confirmation opens on the act it offers, one
// that DESTROYS opens on the way out. What is pinned here is the face that holds the cursor once
// the dialog is open, and the order and rank of the two faces that make "the way out" and "the
// act" mean something — the keystroke itself is a browser's, and this repository has no browser
// suite (the Blazor half pins the same mark, NsConfirmFocusTests).
//
// The DOM is happy-dom and not jsdom, where showModal() is not a function — and the surface under
// test IS a native <dialog> opened by that call.

// act() refuses to drive a renderer unless the environment says it is a test one, and this flag is
// the only door: react-dom/test-utils is gone in React 19.
(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

type Confirm = (text: string, danger?: boolean) => Promise<boolean>;

const question = "¿Eliminar este Color?";
const mounted: { root: Root; host: HTMLElement }[] = [];

function Hand({ onReady }: { onReady: (confirm: Confirm) => void }) {
  const confirm = useConfirm();

  useEffect(() => {
    onReady(confirm);
  }, [confirm, onReady]);

  return null;
}

function surface(children: ReactNode) {
  return (
    <StackProvider mediator={new Mediator()} strings={new Strings("es", stackStrings("es"))}>
      <NsDialogHost>{children}</NsDialogHost>
    </StackProvider>
  );
}

// The real host, asked the way a screen asks it: the promise completes when the dialog closes, so
// awaiting it here would hang the test.
async function ask(danger: boolean) {
  const host = document.createElement("div");
  document.body.append(host);

  const root = createRoot(host);
  mounted.push({ root, host });

  let confirm!: Confirm;

  await act(async () => {
    root.render(surface(<Hand onReady={(ready) => (confirm = ready)} />));
  });

  let answer!: Promise<boolean>;

  await act(async () => {
    answer = confirm(question, danger);
  });

  return {
    dialog: host.querySelector("dialog") as HTMLDialogElement,
    faces: [...host.querySelectorAll<HTMLButtonElement>(".ns-actions button")],
    answer,
  };
}

afterEach(async () => {
  for (const { root, host } of mounted.splice(0)) {
    await act(async () => {
      root.unmount();
    });

    host.remove();
  }
});

describe("the one confirmation surface", () => {
  // The order the house draws, the way out first and the act second, which is what makes the two
  // answers below say something.
  it.each([false, true])("draws the way out before the act (danger: %s)", async (danger) => {
    const { faces } = await ask(danger);

    expect(faces.map((face) => face.textContent)).toEqual(["Cancelar", "Confirmar"]);
  });

  it("opens an ordinary confirmation on the act it offers", async () => {
    const { faces } = await ask(false);

    expect(document.activeElement).toBe(faces[1]);
  });

  // The one this exists for: a reflex Enter over a destructive question can only decline.
  it("opens a destructive confirmation on the way out", async () => {
    const { faces } = await ask(true);

    expect(document.activeElement).toBe(faces[0]);
  });

  // The cursor and the fill are one derivation answering two questions: where a bare Enter lands,
  // and what pressing that face costs (ui/actions.md).
  it.each([
    [true, "ns-button-danger"],
    [false, "ns-button-primary"],
  ])("draws its act with the rank the act costs (danger: %s)", async (danger, rank) => {
    const { faces } = await ask(danger);

    expect(faces[1].className).toContain(rank);
  });

  it.each([
    [0, false],
    [1, true],
  ])("answers the opener with the face that was pressed and closes (face: %s)", async (face, expected) => {
    const { dialog, faces, answer } = await ask(true);

    await act(async () => {
      faces[face].click();
    });

    expect(await answer).toBe(expected);
    expect(dialog.open).toBe(false);
  });
});
