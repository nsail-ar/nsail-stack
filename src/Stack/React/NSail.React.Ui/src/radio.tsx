// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

import ToggleButton from "@mui/material/ToggleButton";
import ToggleButtonGroup from "@mui/material/ToggleButtonGroup";
import { type Field, useStrings } from "@nsail/stack";

export interface NsRadioGroupProps<TValue extends string> {
  /** A member whose kind is enum: its key names the group, its values are the faces. */
  field: Field;
  value: TValue | null | undefined;
  onChange: (value: TValue) => void;
}

/** A segmented choice over a few enum values, all of them in sight: the faces and their words come
 * from the descriptor, and one is always the answer. */
export function NsRadioGroup<TValue extends string>({ field, value, onChange }: NsRadioGroupProps<TValue>) {
  const strings = useStrings();

  if (!field.enum) {
    throw new Error(`${field.key} is not an enum member.`);
  }

  const type = field.enum;

  return (
    <ToggleButtonGroup
      exclusive
      size="small"
      color="primary"
      aria-label={strings.translate(field.key)}
      value={value ?? null}
      onChange={(_, next: TValue | null) => {
        if (next !== null) {
          onChange(next);
        }
      }}
    >
      {type.values.map((option) => (
        <ToggleButton key={option} value={option}>
          {strings.enumValue(type, option)}
        </ToggleButton>
      ))}
    </ToggleButtonGroup>
  );
}
