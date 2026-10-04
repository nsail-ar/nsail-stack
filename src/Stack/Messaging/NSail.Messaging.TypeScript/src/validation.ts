// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

import { memberOf, type Field, type MessageType } from "./descriptors";
import type { Issue, Problem } from "./problems";

// MessageValidator's vocabulary, code for code and argument for argument: the sentence under a
// field reads the same whether the browser refused the value or the server did.

const email = /^[^@\s]+@[^@\s]+$/;
const phone = /^\+?(?=.*\d)[\d\s().\-]+((x|ext\.?)\s?\d+)?$/i;
const url = /^(https?|ftp):\/\//i;

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

  if ((field.min !== undefined || field.max !== undefined) && typeof value === "number") {
    if ((field.min !== undefined && value < field.min) || (field.max !== undefined && value > field.max)) {
      issues.push(issue("OutOfRange", `The field ${name} must be between ${field.min} and ${field.max}`, name, { from: String(field.min), to: String(field.max) }));
    }
  }

  if (typeof value === "string" && value.length > 0) {
    const formatted =
      (field.format === "email" && !email.test(value)) ||
      (field.format === "phone" && !phone.test(value)) ||
      (field.format === "url" && !url.test(value)) ||
      (field.pattern !== undefined && !new RegExp(`^(?:${field.pattern})$`).test(value));

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
