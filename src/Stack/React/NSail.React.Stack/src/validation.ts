// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

import { memberOf, type Field, type MessageType } from "./descriptors";
import type { Issue, Problem } from "./problems";

// MessageValidator's vocabulary, code for code and argument for argument: the sentence under a
// field reads the same whether the browser refused the value or the server did.

const email = /^[^@\s]+@[^@\s]+$/;
const phone = /^\+?(?=.*\d)[\d\s().\-]+((x|ext\.?)\s?\d+)?$/i;
const url = /^(https?|ftp):\/\//i;
// What the server's invariant parse of a long or a decimal takes: blanks around, a sign, no
// group separator, a point. A value past the type's range passes here and is the server's 400.
const exact: Record<string, RegExp> = {
  bigint: /^\s*[+-]?\d+\s*$/,
  decimal: /^\s*[+-]?(\d+\.?\d*|\.\d+)([eE][+-]?\d+)?\s*$/,
};

// Compiled once per pattern, with `u` so \p{L} is a property class; a pattern `u` refuses (a
// loose `\-` outside a class) is tried without it. A pattern JavaScript cannot compile is null: the server's rule alone judges it.
const compiled = new Map<string, RegExp | null>();

function build(pattern: string, flags: string): RegExp | null {
  try {
    return new RegExp(`^(?:${pattern})$`, flags);
  } catch {
    return null;
  }
}

function compile(pattern: string): RegExp | null {
  let regex = compiled.get(pattern);

  if (regex === undefined) {
    regex = build(pattern, "u") ?? build(pattern, "");

    compiled.set(pattern, regex);
  }

  return regex;
}

function issue(code: string, message: string, field: string, args: Record<string, string> = {}): Issue {
  return { code, message, source: field, arguments: { field, ...args } };
}

function isBlank(value: unknown): boolean {
  return value === null || value === undefined || (typeof value === "string" && value.trim().length === 0);
}

function length(value: unknown): number | null {
  if (typeof value === "string" || Array.isArray(value)) {
    return value.length;
  }

  return null;
}

// A bigint compares as a BigInt, whole past 2^53; a decimal's text as the Number it rounds to.
function outside(field: Field, value: unknown): boolean {
  if (field.kind === "bigint" && typeof value === "string" && exact.bigint.test(value)) {
    const whole = BigInt(value.trim());

    return (
      (field.min !== undefined && (Number.isInteger(field.min) ? whole < BigInt(field.min) : Number(whole) < field.min)) ||
      (field.max !== undefined && (Number.isInteger(field.max) ? whole > BigInt(field.max) : Number(whole) > field.max))
    );
  }

  const quantity = typeof value === "number" ? value : field.kind === "decimal" && typeof value === "string" && value.trim() !== "" ? Number(value) : NaN;

  if (Number.isNaN(quantity)) {
    return false;
  }

  return (field.min !== undefined && quantity < field.min) || (field.max !== undefined && quantity > field.max);
}

export function validateField(field: Field, value: unknown): Issue[] {
  const name = memberOf(field);
  const issues: Issue[] = [];

  if (field.required && isBlank(value)) {
    issues.push(issue("Required", `The field ${name} is required`, name));
  }

  // Every BCL attribute but Required passes a null: absence is Required's question alone.
  if (value === null || value === undefined) {
    return issues;
  }

  const size = length(value);

  if (field.maxLength !== undefined && size !== null && size > field.maxLength) {
    issues.push(issue("MaxLength", `The field ${name} exceeds the maximum length of ${field.maxLength}`, name, { max: String(field.maxLength) }));
  }

  if (field.minLength !== undefined && size !== null && size < field.minLength) {
    issues.push(issue("Invalid", `The field ${name} must have at least ${field.minLength} characters`, name));
  }

  if ((field.min !== undefined || field.max !== undefined) && outside(field, value)) {
    issues.push(issue("OutOfRange", `The field ${name} must be between ${field.min} and ${field.max}`, name, { from: String(field.min), to: String(field.max) }));
  }

  if (typeof value === "string" && value.length > 0) {
    const formatted =
      (exact[field.kind] !== undefined && !exact[field.kind].test(value)) ||
      (field.format === "email" && !email.test(value)) ||
      (field.format === "phone" && !phone.test(value)) ||
      (field.format === "url" && !url.test(value)) ||
      (field.pattern !== undefined && compile(field.pattern)?.test(value) === false);

    if (formatted) {
      issues.push(issue("InvalidFormat", `The field ${name} has an invalid format`, name));
    }
  }

  return issues;
}

/** The problem describing every rule the request breaks, or null — the client half of the
 * check SendPipeline runs on both sides. */
export function validate<TRequest>(message: MessageType<TRequest, unknown>, request: TRequest): Problem | null {
  const issues: Issue[] = [];
  const values = request as Record<string, unknown>;

  for (const [name, field] of Object.entries(message.fields) as [string, Field][]) {
    issues.push(...validateField(field, values[name]));
  }

  if (issues.length === 0) {
    return null;
  }

  return { code: "InvalidModel", title: "One or more fields contain invalid data", issues, status: 400 };
}
