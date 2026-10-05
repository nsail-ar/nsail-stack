// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

export type FieldKind =
  | "string"
  | "guid"
  | "boolean"
  | "integer"
  | "number"
  | "date"
  | "datetime"
  | "time"
  | "duration"
  | "enum"
  | "array"
  | "map"
  | "object"
  | "unknown";

export type Binding = "route" | "query" | "header" | "body";

export type Method = "GET" | "POST" | "PUT" | "DELETE" | "PATCH" | "HEAD" | "OPTIONS";

export interface EnumType<TValue extends string = string> {
  readonly key: string;
  readonly values: readonly TValue[];
}

/** What the generator knows about one member: its wire kind, its localization key, how it
 * binds and which of the message's DataAnnotations it carries. */
export interface Field {
  readonly kind: FieldKind;
  readonly key: string;
  readonly nullable?: true;
  readonly enum?: EnumType;
  readonly element?: FieldKind;
  readonly binding?: Binding;
  readonly required?: true;
  readonly maxLength?: number;
  readonly minLength?: number;
  readonly min?: number;
  readonly max?: number;
  readonly format?: "email" | "phone" | "url";
  readonly pattern?: string;
  /** Validation attributes only the server judges — a coded rule, or one the generator could
   * not construct — named without their "Attribute" suffix. */
  readonly serverRules?: readonly string[];
}

export type Fields<T> = { readonly [K in keyof T]-?: Field };

export interface MessageType<TRequest, TResult> {
  readonly name: string;
  readonly key: string;
  readonly area: string | null;
  readonly method: Method;
  readonly path: string;
  readonly fields: Fields<TRequest>;
  /** Never set: carries the result type so send() can infer it. */
  readonly __result?: TResult;
}

export interface ModelType<T> {
  readonly key: string;
  readonly fields: Fields<T>;
}

export type RequestOf<M> = M extends MessageType<infer TRequest, unknown> ? TRequest : never;

export type ResultOf<M> = M extends MessageType<unknown, infer TResult> ? TResult : never;

export function defineEnum<TValue extends string>(key: string, values: readonly TValue[]): EnumType<TValue> {
  return { key, values };
}

export function defineModel<T>(key: string, fields: Fields<T>): ModelType<T> {
  return { key, fields };
}

export function defineMessage<TRequest, TResult>(
  descriptor: Omit<MessageType<TRequest, TResult>, "__result">,
): MessageType<TRequest, TResult> {
  return descriptor;
}

/** The C# member a field was generated from — the last segment of its key, which is what a
 * server Issue names in its source. */
export function memberOf(field: Field): string {
  return field.key.slice(field.key.lastIndexOf(".") + 1);
}
