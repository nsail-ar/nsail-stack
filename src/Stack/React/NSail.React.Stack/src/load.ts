// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

import { useCallback, useEffect, useRef, useState } from "react";
import type { MessageType } from "./descriptors";
import { BusinessError, requestFailed, type Problem } from "./problems";
import { useMediator } from "./context";

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

