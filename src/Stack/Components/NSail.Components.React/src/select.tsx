// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

import type { Field } from "@nsail/messaging";
import { useStrings } from "./context";

export interface NsEnumSelectProps<TValue extends string> {
  /** A member whose kind is enum: its key labels the select, its values are the options. */
  field: Field;
  value: TValue | null | undefined;
  onChange: (value: TValue | null) => void;
  /** A string key: what "no value" reads as, offered when the member is nullable. */
  none?: string;
}

/** A filter or a pick over an enum member, outside a form: the options and their words come
 * from the descriptor, so the page never lists them itself. */
export function NsEnumSelect<TValue extends string>({ field, value, onChange, none = "Common.All" }: NsEnumSelectProps<TValue>) {
  const strings = useStrings();

  if (!field.enum) {
    throw new Error(`${field.key} is not an enum member.`);
  }

  const type = field.enum;

  return (
    <select
      className="ns-select"
      aria-label={strings.translate(field.key)}
      value={value ?? ""}
      onChange={(event) => onChange((event.target.value || null) as TValue | null)}
    >
      {field.nullable && <option value="">{strings.translate(none)}</option>}
      {type.values.map((option) => (
        <option key={option} value={option}>
          {strings.enumValue(type, option)}
        </option>
      ))}
    </select>
  );
}
