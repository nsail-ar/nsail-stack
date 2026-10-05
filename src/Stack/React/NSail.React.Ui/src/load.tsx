// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

import type { ReactNode } from "react";
import { useStrings, type Load } from "@nsail/stack";
import { NsButton } from "./button";

export interface NsLoadProps<T> {
  load: Load<T>;
  children: (data: T) => ReactNode;
}

/** A region that owns its read: a spinner the first time, the last answer (dimmed) while it
 * re-reads, and a failure in place with a retry — never a toast, never an empty state that
 * lies about a read that failed. */
export function NsLoad<T>({ load, children }: NsLoadProps<T>) {
  const strings = useStrings();

  if (load.state === "failed") {
    return (
      <div className="ns-failure" role="alert">
        <strong>{strings.problem(load.problem)}</strong>
        {load.problem.issues.map((issue, index) => (
          <div key={index}>{strings.issue(issue)}</div>
        ))}
        <NsButton text="Common.Retry" onClick={load.reload} />
      </div>
    );
  }

  if (load.data === undefined) {
    return <div className="ns-loading" aria-busy="true">{strings.translate("Common.Loading")}…</div>;
  }

  return <div className={load.state === "loading" ? "ns-region ns-stale" : "ns-region"}>{children(load.data)}</div>;
}
