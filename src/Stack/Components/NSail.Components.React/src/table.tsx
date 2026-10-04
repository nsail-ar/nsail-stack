// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

import type { ReactNode } from "react";
import { memberOf, type Field, type ModelType } from "@nsail/messaging";
import { useStrings } from "./context";
import { display } from "./format";

export interface NsTableProps<T> {
  model: ModelType<T>;
  rows: readonly T[];
  columns: readonly (keyof T & string)[];
  rowKey: keyof T & string;
  onRowClick?: (row: T) => void;
  actions?: (row: T) => ReactNode;
  /** A string key: what an empty answer says. */
  empty?: string;
}

/** Rows of a projection: each header is the member's key, each cell its value formatted by kind. */
export function NsTable<T>({ model, rows, columns, rowKey, onRowClick, actions, empty = "Common.NoRecords" }: NsTableProps<T>) {
  const strings = useStrings();

  if (rows.length === 0) {
    return <div className="ns-empty">{strings.translate(empty)}</div>;
  }

  return (
    <div className="ns-table-scroll">
      <table className="ns-table">
        <thead>
          <tr>
            {columns.map((column) => {
              const field: Field = model.fields[column];

              return (
                <th key={column} className={`ns-col-${field.kind}`}>
                  {strings.translate(field.key, memberOf(field))}
                </th>
              );
            })}
            {actions && <th className="ns-col-actions" />}
          </tr>
        </thead>
        <tbody>
          {rows.map((row) => (
            <tr key={String(row[rowKey])} className={onRowClick ? "ns-clickable" : undefined} onClick={onRowClick ? () => onRowClick(row) : undefined}>
              {columns.map((column) => {
                const field: Field = model.fields[column];

                return (
                  <td key={column} className={`ns-col-${field.kind}`}>
                    {display(strings, field, row[column])}
                  </td>
                );
              })}
              {actions && (
                <td className="ns-col-actions" onClick={(event) => event.stopPropagation()}>
                  {actions(row)}
                </td>
              )}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
