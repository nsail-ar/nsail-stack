// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

import { useCallback, useEffect, useRef, useState, type ReactNode } from "react";
import { BusinessError, requestFailed, type MessageType, type Problem } from "@nsail/messaging";
import { useMediator, useStrings } from "./context";
import { NsButton } from "./button";

export type Load<T> =
  | { state: "loading"; data: T | undefined; problem: null; reload: () => void }
  | { state: "loaded"; data: T; problem: null; reload: () => void }
  | { state: "failed"; data: T | undefined; problem: Problem; reload: () => void };

export function problemOf(error: unknown): Problem {
  return error instanceof BusinessError ? error.problem : requestFailed(null, null);
}

/** Reads a query and keeps it current: the request is compared by value, so a new object with
 * the same filters does not read twice, and an answer that lost the race is dropped. */
export function useLoad<TRequest, TResult>(message: MessageType<TRequest, TResult>, request: TRequest): Load<TResult> {
  const mediator = useMediator();
  const identity = JSON.stringify(request);
  const [version, setVersion] = useState(0);
  const [load, setLoad] = useState<{ state: "loading" | "loaded" | "failed"; data: TResult | undefined; problem: Problem | null }>({
    state: "loading",
    data: undefined,
    problem: null,
  });
  const latest = useRef(request);

  latest.current = request;

  useEffect(() => {
    const abort = new AbortController();

    setLoad((current) => ({ state: "loading", data: current.data, problem: null }));

    mediator
      .send(message, latest.current, { signal: abort.signal })
      .then((data) => setLoad({ state: "loaded", data, problem: null }))
      .catch((error: unknown) => {
        if (!abort.signal.aborted) {
          setLoad((current) => ({ state: "failed", data: current.data, problem: problemOf(error) }));
        }
      });

    return () => abort.abort();
  }, [mediator, message, identity, version]);

  const reload = useCallback(() => setVersion((v) => v + 1), []);

  return { ...load, reload } as Load<TResult>;
}

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
