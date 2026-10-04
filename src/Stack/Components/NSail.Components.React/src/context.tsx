// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

import { createContext, useContext, useEffect, useState, type ReactNode } from "react";
import { Mediator } from "@nsail/messaging";
import { Strings, stackStrings } from "./strings";
import { NsDialogHost } from "./dialog";

const MediatorContext = createContext<Mediator | null>(null);
const StringsContext = createContext<Strings | null>(null);

export interface NsAppProps {
  mediator: Mediator;
  language: string;
  /** The app's catalog for a language, merged over the Stack's: the app's keys win, as an app's
   * strings registered last win on the server. */
  strings?: (language: string) => Promise<Record<string, string>>;
  children: ReactNode;
}

export function NsApp({ mediator, language, strings, children }: NsAppProps) {
  const [catalog, setCatalog] = useState<Strings | null>(null);
  const [failed, setFailed] = useState(false);

  useEffect(() => {
    let live = true;

    (strings ? strings(language) : Promise.resolve({}))
      .then((entries) => {
        if (live) {
          document.documentElement.lang = language;
          setCatalog(new Strings(language, { ...stackStrings(language), ...entries }));
        }
      })
      .catch(() => {
        // The page still renders, keyed: an app with no catalog is legible, a blank one is not.
        if (live) {
          setFailed(true);
          setCatalog(new Strings(language, stackStrings(language)));
        }
      });

    return () => {
      live = false;
    };
  }, [language, strings]);

  if (!catalog) {
    return <div className="ns-boot" aria-busy="true" />;
  }

  return (
    <MediatorContext.Provider value={mediator}>
      <StringsContext.Provider value={catalog}>
        {failed && <div className="ns-banner ns-banner-error">{catalog.translate("Common.UnhandledError")}</div>}
        <NsDialogHost>{children}</NsDialogHost>
      </StringsContext.Provider>
    </MediatorContext.Provider>
  );
}

export function useMediator(): Mediator {
  const mediator = useContext(MediatorContext);

  if (!mediator) {
    throw new Error("useMediator needs an <NsApp> above it.");
  }

  return mediator;
}

export function useStrings(): Strings {
  const strings = useContext(StringsContext);

  if (!strings) {
    throw new Error("useStrings needs an <NsApp> above it.");
  }

  return strings;
}
