// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

import type { ReactNode } from "react";
import Table from "@mui/material/Table";
import TableBody from "@mui/material/TableBody";
import TableCell from "@mui/material/TableCell";
import TableContainer from "@mui/material/TableContainer";
import TableHead from "@mui/material/TableHead";
import TableRow from "@mui/material/TableRow";
import { memberOf, type Field, type ModelType, useStrings } from "@nsail/stack";
import { NsEmpty } from "./empty";
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

const figures = new Set<Field["kind"]>(["integer", "number", "bigint", "decimal"]);

/** Rows of a projection: each header is the member's key, each cell its value formatted by kind. */
export function NsTable<T>({ model, rows, columns, rowKey, onRowClick, actions, empty = "Common.NoRecords", ungrouped }: NsTableProps<T>) {
  const strings = useStrings();

  if (rows.length === 0) {
    return <NsEmpty text={empty} />;
  }

  return (
    <TableContainer>
      <Table size="small">
        <TableHead>
          <TableRow>
            {columns.map((column) => {
              const field: Field = model.fields[column];

              return (
                <TableCell key={column} align={figures.has(field.kind) ? "right" : "left"}>
                  {strings.translate(field.key, memberOf(field))}
                </TableCell>
              );
            })}
            {actions && <TableCell />}
          </TableRow>
        </TableHead>
        <TableBody>
          {rows.map((row) => (
            <TableRow
              key={String(row[rowKey])}
              hover={!!onRowClick}
              tabIndex={onRowClick ? 0 : undefined}
              sx={onRowClick ? { cursor: "pointer" } : undefined}
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
                  <TableCell key={column} align={figures.has(field.kind) ? "right" : "left"}>
                    {display(strings, field, row[column], { grouped: !ungrouped?.includes(column) })}
                  </TableCell>
                );
              })}
              {actions && (
                <TableCell align="right" onClick={(event) => event.stopPropagation()}>
                  {actions(row)}
                </TableCell>
              )}
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </TableContainer>
  );
}
