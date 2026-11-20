// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

import type { ReactNode } from "react";
import { useSession } from "@nsail/stack";

export interface NsSignedInProps {
  /** Drawn instead when the session is read and nobody is signed in. */
  anonymous?: ReactNode;
  children: ReactNode;
}

/** A region that requires a session: its content is drawn only once the session is read and
 * signed in — never while it is being read, never from a read that failed. */
export function NsSignedIn({ anonymous, children }: NsSignedInProps) {
  const session = useSession();

  if (session.state !== "loaded") {
    return null;
  }

  return session.data.isAuthenticated ? children : (anonymous ?? null);
}
