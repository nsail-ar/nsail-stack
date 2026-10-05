// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

import type { ReactNode } from "react";
import { StackProvider, useCatalog, type Mediator } from "@nsail/stack";
import { NsDialogHost } from "./dialog";

export interface NsAppProps {
  mediator: Mediator;
  language: string;
  /** The app's catalog for a language, merged over the Stack's. */
  strings?: (language: string) => Promise<Record<string, string>>;
  children: ReactNode;
}

/** The app's root: the stack's providers, the catalog read before the first paint, and the one
 * confirmation surface. */
export function NsApp({ mediator, language, strings, children }: NsAppProps) {
  const catalog = useCatalog(language, strings);

  if (!catalog.strings) {
    return <div className="ns-boot" aria-busy="true" />;
  }

  return (
    <StackProvider mediator={mediator} strings={catalog.strings}>
      {catalog.failed && <div className="ns-banner ns-banner-error">{catalog.strings.translate("Common.UnhandledError")}</div>}
      <NsDialogHost>{children}</NsDialogHost>
    </StackProvider>
  );
}
