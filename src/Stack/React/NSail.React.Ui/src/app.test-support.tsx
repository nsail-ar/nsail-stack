// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

import { act, type ReactNode } from "react";
import { createRoot, type Root } from "react-dom/client";
import { Mediator, type Brand } from "@nsail/stack";
import { NsApp } from "./app";

// act() refuses to drive a renderer unless the environment says it is a test one.
(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const mounted: { root: Root; host: HTMLElement }[] = [];

export interface Drawn {
  host: HTMLElement;
  rerender: (node: ReactNode) => Promise<void>;
}

/** A piece drawn the way a client draws it: inside NsApp, with the catalog given merged over the
 * Stack's own sentences in Spanish. `dark: null` leaves the scheme to the device. */
export async function draw(
  node: ReactNode,
  { catalog = {}, brand, dark = false }: { catalog?: Record<string, string>; brand?: Brand; dark?: boolean | null } = {},
): Promise<Drawn> {
  const host = document.body.appendChild(document.createElement("div"));
  const root = createRoot(host);
  const strings = () => Promise.resolve(catalog);
  const mediator = new Mediator();

  mounted.push({ root, host });

  async function render(content: ReactNode) {
    await act(async () => {
      root.render(
        <NsApp mediator={mediator} language="es" strings={strings} brand={brand} dark={dark ?? undefined}>
          {content}
        </NsApp>,
      );
    });
  }

  await render(node);

  return { host, rerender: render };
}

export async function clear() {
  for (const { root, host } of mounted.splice(0)) {
    await act(async () => root.unmount());
    host.remove();
  }
}
