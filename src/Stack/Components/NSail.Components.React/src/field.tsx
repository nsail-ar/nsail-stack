// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

import { useId, type ChangeEvent } from "react";
import { memberOf, type Field } from "@nsail/messaging";
import { useStrings } from "./context";
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

  function text(event: ChangeEvent<HTMLInputElement | HTMLTextAreaElement | HTMLSelectElement>) {
    const raw = event.target.value;

    set(raw === "" && field.nullable ? null : raw);
  }

  function number(event: ChangeEvent<HTMLInputElement>) {
    const raw = event.target.value;

    set(raw === "" ? (field.nullable ? null : undefined) : Number(raw));
  }

  const common = {
    id,
    name,
    autoFocus,
    "aria-invalid": invalid || undefined,
    "aria-describedby": invalid ? `${id}-issues` : undefined,
    required: field.required,
  };

  let control;

  switch (field.kind) {
    case "boolean":
      control = <input {...common} type="checkbox" checked={value === true} onChange={(e) => set(e.target.checked)} />;
      break;
    case "integer":
    case "number":
      control = (
        <input
          {...common}
          type="number"
          min={field.min}
          max={field.max}
          step={field.kind === "integer" ? 1 : "any"}
          value={value === null || value === undefined ? "" : String(value)}
          onChange={number}
        />
      );
      break;
    case "enum":
      control = (
        <select {...common} value={(value as string | null | undefined) ?? ""} onChange={text}>
          {(field.nullable || value === undefined) && <option value="" />}
          {field.enum?.values.map((option) => (
            <option key={option} value={option}>
              {strings.enumValue(field.enum!, option)}
            </option>
          ))}
        </select>
      );
      break;
    case "date":
      control = <input {...common} type="date" value={(value as string | null) ?? ""} onChange={text} />;
      break;
    case "time":
      control = <input {...common} type="time" value={(value as string | null) ?? ""} onChange={text} />;
      break;
    default:
      control = multiline ? (
        <textarea {...common} rows={4} maxLength={field.maxLength} value={(value as string | null) ?? ""} onChange={text} />
      ) : (
        <input
          {...common}
          type={(field.format && inputTypes[field.format]) || "text"}
          maxLength={field.maxLength}
          value={(value as string | null) ?? ""}
          onChange={text}
        />
      );
  }

  return (
    <div className={`ns-field ns-field-${field.kind}${invalid ? " ns-field-invalid" : ""}`}>
      <label htmlFor={id}>
        {caption}
        {field.required && <span className="ns-required" aria-hidden="true"> *</span>}
      </label>
      {control}
      {invalid && (
        <div id={`${id}-issues`} className="ns-issues">
          {issues.map((issue, index) => (
            <div key={index}>{strings.issue(issue)}</div>
          ))}
        </div>
      )}
    </div>
  );
}
