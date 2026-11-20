// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

import { createContext, useContext, useEffect, useState, type ReactNode } from "react";
import type { MessageType } from "./descriptors";
import type { Mediator } from "./mediator";
import { useLoad, type Load } from "./load";
import { Strings, stackStrings } from "./strings";

const MediatorContext = createContext<Mediator | null>(null);
const StringsContext = createContext<Strings | null>(null);
const SessionContext = createContext<Load<SessionShape> | null>(null);

/** What the Stack reads of the answer a client's session message returns (NSail.Security.Session). */
export interface SessionShape {
  isAuthenticated: boolean;
}

export interface StackProviderProps {
  mediator: Mediator;
  strings: Strings;
  /** The message that answers who is signed in, sent with no members (Iam's GetSession). */
  session?: MessageType<Record<never, never>, SessionShape>;
  children: ReactNode;
}

/** What every hook of the stack reads: the mediator that sends, the strings that speak and, when
 * the client names its session message, the session. */
export function StackProvider({ mediator, strings, session, children }: StackProviderProps) {
  return (
    <MediatorContext.Provider value={mediator}>
      <StringsContext.Provider value={strings}>{session ? <SessionReader message={session}>{children}</SessionReader> : children}</StringsContext.Provider>
    </MediatorContext.Provider>
  );
}

const noMembers: Record<never, never> = {};

function SessionReader({ message, children }: { message: MessageType<Record<never, never>, SessionShape>; children: ReactNode }) {
  const load = useLoad(message, noMembers);

  return <SessionContext.Provider value={load}>{children}</SessionContext.Provider>;
}

/** Who is signed in, read once for the whole client. `reload()` is how the client reports a
 * sign-in or a sign-out it made, so every reader sees the new session. */
export function useSession<TSession extends SessionShape = SessionShape>(): Load<TSession> {
  const session = useContext(SessionContext);

  if (!session) {
    throw new Error("useSession needs a <StackProvider session={…}> above it.");
  }

  return session as Load<TSession>;
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
