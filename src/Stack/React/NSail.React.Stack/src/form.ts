// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

import { useCallback, useMemo, useState } from "react";
import { memberOf, type Field, type MessageType } from "./descriptors";
import type { Issue, Problem } from "./problems";
import { validate, validateField } from "./validation";
import { problemOf } from "./load";

export interface Form<T> {
  readonly message: MessageType<T, unknown>;
  readonly value: T;
  readonly submitting: boolean;
  /** The last send landed and nothing changed since. */
  readonly saved: boolean;
  /** What the server last refused, kept until the field it is about changes. */
  readonly problem: Problem | null;
  set<K extends keyof T>(name: K, value: T[K]): void;
  reset(value: T): void;
  issues(name: keyof T): Issue[];
  submit(action: (value: T) => Promise<void>): Promise<boolean>;
}

/** The state of one message being filled in: its value, its refusals (the descriptor's rules
 * checked as the user types, the server's answer after a send) and whether a send is running. */
export function useForm<T>(message: MessageType<T, unknown>, initial: T): Form<T> {
  const [value, setValue] = useState<T>(initial);
  const [touched, setTouched] = useState<ReadonlySet<keyof T>>(new Set());
  const [problem, setProblem] = useState<Problem | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [saved, setSaved] = useState(false);

  const set = useCallback(<K extends keyof T>(name: K, next: T[K]) => {
    setValue((current) => ({ ...current, [name]: next }));
    setTouched((current) => new Set(current).add(name));
    setSaved(false);

    const member = memberOf(message.fields[name]);

    setProblem((current) => (current ? { ...current, issues: current.issues.filter((issue) => issue.source !== member) } : current));
  }, [message]);

  const reset = useCallback((next: T) => {
    setValue(next);
    setTouched(new Set());
    setProblem(null);
  }, []);

  const issues = useCallback(
    (name: keyof T) => {
      const field: Field = message.fields[name];
      const member = memberOf(field);
      const local = touched.has(name) ? validateField(field, (value as Record<string, unknown>)[name as string]) : [];
      const remote = problem?.issues.filter((issue) => issue.source === member) ?? [];

      return [...local, ...remote.filter((r) => !local.some((l) => l.code === r.code))];
    },
    [message, touched, value, problem],
  );

  const submit = useCallback(
    async (action: (value: T) => Promise<void>) => {
      const refused = validate(message, value);

      if (refused) {
        setTouched(new Set(Object.keys(message.fields) as (keyof T)[]));
        setProblem(refused);

        return false;
      }

      setSubmitting(true);
      setSaved(false);
      setProblem(null);

      try {
        await action(value);
        setSaved(true);

        return true;
      } catch (error) {
        setProblem(problemOf(error));

        return false;
      } finally {
        setSubmitting(false);
      }
    },
    [message, value],
  );

  return useMemo(
    () => ({ message, value, submitting, saved, problem, set, reset, issues, submit }),
    [message, value, submitting, saved, problem, set, reset, issues, submit],
  );
}

