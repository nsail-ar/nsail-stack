// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

import { createContext, useCallback, useContext, useMemo, useState, type FormEvent, type ReactNode } from "react";
import { memberOf, validate, validateField, type Field, type Issue, type MessageType, type Problem } from "@nsail/messaging";
import { problemOf } from "./load";
import { useStrings } from "./context";
import { NsButton } from "./button";

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

const FormContext = createContext<Form<unknown> | null>(null);

export function useFormContext<T>(): Form<T> {
  const form = useContext(FormContext);

  if (!form) {
    throw new Error("An NsField needs an <NsForm> around it.");
  }

  return form as Form<T>;
}

/** "Saved" beside the form's buttons, for as long as nothing has changed since the send. */
export function NsSaved() {
  const form = useFormContext<unknown>();
  const strings = useStrings();

  return form.saved ? (
    <span className="ns-saved" role="status">
      {strings.translate("Common.Saved")}
    </span>
  ) : null;
}

export interface NsFormProps<T> {
  form: Form<T>;
  onSubmit: (value: T) => Promise<void>;
  /** The screen's first read, run again: offered only while the refusal is a Conflict, because
   * re-applying the edit over what someone else saved is the user's act, never a merge. */
  onReload?: () => void;
  children: ReactNode;
}

export function NsForm<T>({ form, onSubmit, onReload, children }: NsFormProps<T>) {
  const strings = useStrings();

  function handle(event: FormEvent) {
    event.preventDefault();
    void form.submit(onSubmit);
  }

  // A refusal no field can show — a missing row, a rule about the whole act, a lost
  // connection — is drawn above the fields instead of vanishing.
  const members = new Set(Object.values<Field>(form.message.fields as Record<string, Field>).map(memberOf));
  const loose = form.problem?.issues.filter((issue) => !issue.source || !members.has(issue.source)) ?? [];
  const banner = form.problem && (form.problem.code !== "InvalidModel" || loose.length > 0);

  return (
    <FormContext.Provider value={form as Form<unknown>}>
      <form className="ns-form" onSubmit={handle} noValidate>
        {banner && form.problem && (
          <div className="ns-banner ns-banner-error" role="alert">
            <strong>{strings.problem(form.problem)}</strong>
            {loose.map((issue, index) => (
              <div key={index}>{strings.issue(issue)}</div>
            ))}
            {onReload && form.problem.code === "Conflict" && <NsButton text="Common.Reload" onClick={onReload} />}
          </div>
        )}
        {children}
      </form>
    </FormContext.Provider>
  );
}
