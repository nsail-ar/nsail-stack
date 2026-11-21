// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

import { createContext, useContext, type FormEvent, type ReactNode } from "react";
import Alert from "@mui/material/Alert";
import AlertTitle from "@mui/material/AlertTitle";
import Stack from "@mui/material/Stack";
import Typography from "@mui/material/Typography";
import { memberOf, useStrings, type Field, type Form } from "@nsail/stack";
import { NsButton } from "./button";

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
    <Typography component="span" variant="body2" sx={{ color: "ink.success" }} role="status">
      {strings.translate("Common.Saved")}
    </Typography>
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
      <Stack component="form" spacing={2} onSubmit={handle} noValidate>
        {banner && form.problem && (
          <Alert
            severity="error"
            role="alert"
            action={onReload && form.problem.code === "Conflict" ? <NsButton text="Common.Reload" variant="quiet" onClick={onReload} /> : undefined}
          >
            <AlertTitle>{strings.problem(form.problem)}</AlertTitle>
            {loose.map((issue, index) => (
              <div key={index}>{strings.issue(issue)}</div>
            ))}
          </Alert>
        )}
        {children}
      </Stack>
    </FormContext.Provider>
  );
}
