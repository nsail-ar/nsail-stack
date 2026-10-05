// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

import { useEffect, type ReactNode } from "react";
import { useStrings } from "@nsail/stack";

export interface NsPageProps {
  /** A string key. */
  title: string;
  actions?: ReactNode;
  children: ReactNode;
}

export function NsPage({ title, actions, children }: NsPageProps) {
  const strings = useStrings();
  const text = strings.translate(title);

  useEffect(() => {
    document.title = text;
  }, [text]);

  return (
    <main className="ns-page">
      <header className="ns-page-header">
        <h1>{text}</h1>
        {actions && <div className="ns-actions">{actions}</div>}
      </header>
      {children}
    </main>
  );
}
