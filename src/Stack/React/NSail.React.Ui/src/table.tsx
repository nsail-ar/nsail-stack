// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

import type { ReactNode } from "react";
import { memberOf, type Field, type ModelType, useStrings } from "@nsail/stack";
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
  /** Columns shown without digit grouping: a number that names something (a voucher), not a quantity. */
  ungrouped?: readonly (keyof T & string)[];
}

/** Rows of a projection: each header is the member's key, each cell its value formatted by kind. */
export function NsTable<T>({ model, rows, columns, rowKey, onRowClick, actions, empty = "Common.NoRecords", ungrouped }: NsTableProps<T>) {
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
            <tr
              key={String(row[rowKey])}
              className={onRowClick ? "ns-clickable" : undefined}
              tabIndex={onRowClick ? 0 : undefined}
              onClick={onRowClick ? () => onRowClick(row) : undefined}
              onKeyDown={
                onRowClick
                  ? (event) => {
                      if (event.target === event.currentTarget && (event.key === "Enter" || event.key === " ")) {
                        event.preventDefault();
                        onRowClick(row);
                      }
                    }
                  : undefined
              }
            >
              {columns.map((column) => {
                const field: Field = model.fields[column];

                return (
                  <td key={column} className={`ns-col-${field.kind}`}>
                    {display(strings, field, row[column], { grouped: !ungrouped?.includes(column) })}
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
