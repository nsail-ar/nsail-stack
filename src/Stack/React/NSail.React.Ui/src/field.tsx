// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

import { useId, type ChangeEvent } from "react";
import Checkbox from "@mui/material/Checkbox";
import FormControl from "@mui/material/FormControl";
import FormControlLabel from "@mui/material/FormControlLabel";
import FormHelperText from "@mui/material/FormHelperText";
import TextField from "@mui/material/TextField";
import { memberOf, type Field, useStrings } from "@nsail/stack";
import { useFormContext } from "./form";

export interface NsFieldProps<T> {
  name: keyof T & string;
  multiline?: boolean;
  autoFocus?: boolean;
  /** A string key replacing the one the field's own key derives. */
  label?: string;
}

const inputTypes: Partial<Record<string, string>> = { email: "email", phone: "tel", url: "url" };

/** One member of the form's message, drawn from its descriptor: the input fits the wire kind,
 * the label is the member's key, the bounds are the message's own DataAnnotations. */
export function NsField<T>({ name, multiline = false, autoFocus, label }: NsFieldProps<T>) {
  const form = useFormContext<T>();
  const strings = useStrings();
  const id = useId();
  const field: Field = form.message.fields[name];
  const value = (form.value as Record<string, unknown>)[name];
  const issues = form.issues(name);
  const caption = strings.translate(label ?? field.key, memberOf(field));
  const invalid = issues.length > 0;

  function set(next: unknown) {
    form.set(name, next as T[keyof T & string]);
  }

  function text(event: ChangeEvent<HTMLInputElement | HTMLTextAreaElement>) {
    const raw = event.target.value;

    set(raw === "" && field.nullable ? null : raw);
  }

  function number(event: ChangeEvent<HTMLInputElement | HTMLTextAreaElement>) {
    const raw = event.target.value;

    if (raw !== "" && field.kind === "integer" && !Number.isInteger(Number(raw))) {
      return;
    }

    set(raw === "" ? (field.nullable ? null : undefined) : Number(raw));
  }

  // An emptied exact number is absent, as a number is: "" is no long the server reads.
  function exact(event: ChangeEvent<HTMLInputElement | HTMLTextAreaElement>) {
    const raw = event.target.value;

    set(raw === "" ? (field.nullable ? null : undefined) : raw);
  }

  const helper = invalid ? (
    <span id={`${id}-issues`}>
      {issues.map((issue, index) => (
        <span key={index} style={{ display: "block" }}>
          {strings.issue(issue)}
        </span>
      ))}
    </span>
  ) : undefined;

  if (field.kind === "boolean") {
    return (
      <FormControl error={invalid} required={field.required}>
        <FormControlLabel
          label={caption}
          control={
            <Checkbox
              id={id}
              name={name}
              autoFocus={autoFocus}
              checked={value === true}
              onChange={(event) => set(event.target.checked)}
              slotProps={{ input: { "aria-invalid": invalid || undefined, "aria-describedby": invalid ? `${id}-issues` : undefined } }}
            />
          }
        />
        {helper && <FormHelperText component="div">{helper}</FormHelperText>}
      </FormControl>
    );
  }

  const shown = value === null || value === undefined ? "" : String(value);
  const common = {
    id,
    name,
    autoFocus,
    fullWidth: true,
    label: caption,
    error: invalid,
    required: field.required,
    helperText: helper,
    slotProps: { formHelperText: { component: "div" } as object, inputLabel: { shrink: true } },
  };

  switch (field.kind) {
    case "bigint":
    case "decimal":
      // The value stays the text the user typed: a Number would round it.
      return <TextField {...common} type="text" value={shown} onChange={exact} slotProps={{ ...common.slotProps, htmlInput: { inputMode: field.kind === "bigint" ? "numeric" : "decimal" } }} />;
    case "integer":
    case "number":
      return (
        <TextField
          {...common}
          type="number"
          value={shown}
          onChange={number}
          slotProps={{ ...common.slotProps, htmlInput: { min: field.min, max: field.max, step: field.kind === "integer" ? 1 : "any" } }}
        />
      );
    case "enum":
      return (
        <TextField {...common} select value={(value as string | null | undefined) ?? ""} onChange={text} slotProps={{ ...common.slotProps, select: { native: true } }}>
          {(field.nullable || value === undefined) && <option value="" />}
          {field.enum?.values.map((option) => (
            <option key={option} value={option}>
              {strings.enumValue(field.enum!, option)}
            </option>
          ))}
        </TextField>
      );
    case "date":
      return <TextField {...common} type="date" value={shown} onChange={text} />;
    case "time":
      return <TextField {...common} type="time" value={shown} onChange={text} />;
    default:
      return (
        <TextField
          {...common}
          type={multiline ? undefined : (field.format && inputTypes[field.format]) || "text"}
          multiline={multiline}
          minRows={multiline ? 4 : undefined}
          value={shown}
          onChange={text}
          slotProps={{ ...common.slotProps, htmlInput: { maxLength: field.maxLength } }}
        />
      );
  }
}
