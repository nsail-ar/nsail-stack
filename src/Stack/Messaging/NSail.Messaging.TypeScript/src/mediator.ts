// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

import type { Field, MessageType } from "./descriptors";
import { BusinessError, isProblem, requestFailed } from "./problems";
import { validate } from "./validation";

export interface MediatorOptions {
  /** Origin the API lives at; empty means the host that served the page. */
  baseUrl?: string;
  fetch?: typeof fetch;
  /** Headers every send carries (a language, a tenant). Read per send. */
  headers?: () => Record<string, string>;
}

export interface SendOptions {
  signal?: AbortSignal;
}

function format(value: unknown): string {
  return typeof value === "string" ? value : String(value);
}

/** Sends a message to the endpoint its [Http] declares: the client half of HttpSender. The
 * request's own DataAnnotations are checked before anything leaves, as SendPipeline does. */
export class Mediator {
  readonly #baseUrl: string;
  readonly #fetch: typeof fetch;
  readonly #headers: () => Record<string, string>;

  constructor(options: MediatorOptions = {}) {
    this.#baseUrl = (options.baseUrl ?? "").replace(/\/+$/, "");
    this.#fetch = options.fetch ?? globalThis.fetch.bind(globalThis);
    this.#headers = options.headers ?? (() => ({}));
  }

  async send<TRequest, TResult>(message: MessageType<TRequest, TResult>, request: TRequest, options: SendOptions = {}): Promise<TResult> {
    const refused = validate(message, request);

    if (refused) {
      throw new BusinessError(refused);
    }

    let response: Response;

    try {
      response = await this.#fetch(this.url(message, request), {
        method: message.method,
        headers: { Accept: "application/json", ...this.#headers(), ...this.headersOf(message, request), ...(this.hasBody(message) ? { "Content-Type": "application/json" } : {}) },
        body: this.hasBody(message) ? JSON.stringify(this.bodyOf(message, request)) : undefined,
        signal: options.signal,
      });
    } catch (error) {
      if (error instanceof DOMException && error.name === "AbortError") {
        throw error;
      }

      throw new BusinessError(requestFailed(null, null));
    }

    const text = await response.text();

    if (!response.ok) {
      throw new BusinessError(this.problemOf(response.status, text));
    }

    if (text.length === 0) {
      return undefined as TResult;
    }

    return JSON.parse(text) as TResult;
  }

  url<TRequest>(message: MessageType<TRequest, unknown>, request: TRequest): string {
    const values = request as Record<string, unknown>;
    const fields = Object.entries(message.fields) as [string, Field][];

    const path = message.path.replace(/{([^{}:?]+)[^{}]*}/g, (_, token: string) => {
      const name = fields.find(([candidate]) => candidate.toLowerCase() === token.toLowerCase())?.[0] ?? token;

      return encodeURIComponent(format(values[name]));
    });

    const query = new URLSearchParams();

    for (const [name, field] of fields) {
      const value = values[name];

      // An unset filter is an absent parameter, never "null" (HttpRequestMessageBuilder.AddQuery).
      if (field.binding !== "query" || value === null || value === undefined) {
        continue;
      }

      if (Array.isArray(value)) {
        value.filter((item) => item !== null && item !== undefined).forEach((item) => query.append(name, format(item)));
      } else {
        query.append(name, format(value));
      }
    }

    const search = query.toString();

    return `${this.#baseUrl}/${path}${search ? `?${search}` : ""}`;
  }

  private hasBody(message: MessageType<unknown, unknown>): boolean {
    return message.method === "POST" || message.method === "PUT" || message.method === "PATCH";
  }

  private bodyOf<TRequest>(message: MessageType<TRequest, unknown>, request: TRequest): Record<string, unknown> {
    const values = request as Record<string, unknown>;
    const body: Record<string, unknown> = {};

    for (const [name, field] of Object.entries(message.fields) as [string, Field][]) {
      if (field.binding === "body" && values[name] !== undefined) {
        body[name] = values[name];
      }
    }

    return body;
  }

  private headersOf<TRequest>(message: MessageType<TRequest, unknown>, request: TRequest): Record<string, string> {
    const values = request as Record<string, unknown>;
    const headers: Record<string, string> = {};

    for (const [name, field] of Object.entries(message.fields) as [string, Field][]) {
      if (field.binding === "header" && values[name] !== null && values[name] !== undefined) {
        headers[name] = format(values[name]);
      }
    }

    return headers;
  }

  private problemOf(status: number, text: string) {
    if (text.length === 0) {
      return requestFailed(status, "EmptyResponse");
    }

    try {
      const parsed: unknown = JSON.parse(text);

      return isProblem(parsed) ? { ...parsed, issues: parsed.issues ?? [] } : requestFailed(status, "StatusFallback");
    } catch {
      return requestFailed(status, "DeserializationFailed");
    }
  }
}
