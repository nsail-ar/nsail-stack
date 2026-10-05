// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

import { createContext, useContext, useEffect, useState, type ReactNode } from "react";
import type { Mediator } from "./mediator";
import { Strings, stackStrings } from "./strings";

const MediatorContext = createContext<Mediator | null>(null);
const StringsContext = createContext<Strings | null>(null);

export interface StackProviderProps {
  mediator: Mediator;
  strings: Strings;
  children: ReactNode;
}

/** What every hook of the stack reads: the mediator that sends and the strings that speak. */
export function StackProvider({ mediator, strings, children }: StackProviderProps) {
  return (
    <MediatorContext.Provider value={mediator}>
      <StringsContext.Provider value={strings}>{children}</StringsContext.Provider>
    </MediatorContext.Provider>
  );
}

export interface Catalog {
  strings: Strings | null;
  failed: boolean;
}

/** The catalog for a language: the Stack's sentences with the app's merged over them — the app's
 * keys win, as an app's strings registered last win on the server. A failed read still yields
 * the Stack's own, so the page renders keyed rather than blank. */
export function useCatalog(language: string, load?: (language: string) => Promise<Record<string, string>>): Catalog {
  const [catalog, setCatalog] = useState<Catalog>({ strings: null, failed: false });

  useEffect(() => {
    let live = true;

    (load ? load(language) : Promise.resolve({}))
      .then((entries) => {
        if (live) {
          document.documentElement.lang = language;
          setCatalog({ strings: new Strings(language, { ...stackStrings(language), ...entries }), failed: false });
        }
      })
      .catch(() => {
        if (live) {
          setCatalog({ strings: new Strings(language, stackStrings(language)), failed: true });
        }
      });

    return () => {
      live = false;
    };
  }, [language, load]);

  return catalog;
}

export function useMediator(): Mediator {
  const mediator = useContext(MediatorContext);

  if (!mediator) {
    throw new Error("useMediator needs a <StackProvider> above it.");
  }

  return mediator;
}

export function useStrings(): Strings {
  const strings = useContext(StringsContext);

  if (!strings) {
    throw new Error("useStrings needs a <StackProvider> above it.");
  }

  return strings;
}
