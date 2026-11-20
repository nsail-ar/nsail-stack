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
  /** Address on the page's host a send answered 401 turns to, carrying where the visitor was
   * on `returnUrl`. From the client's route table; absent, a 401 is a refusal like any other. */
  signIn?: string;
  /** Where the visitor is and how the turn is made; the page's own `location` by default. */
  location?: Pick<Location, "pathname" | "search" | "hash" | "assign">;
}

export interface SendOptions {
  signal?: AbortSignal;
}

// SignInRoutes.ReturnUrlParameter and StaleParameter: the spellings Iam's sign-in page reads and
// NsNotAuthorized writes.
const returnUrlParameter = "returnUrl";
const staleParameter = "staleCredential";

function format(value: unknown): string {
  return typeof value === "string" ? value : String(value);
}

/** Sends a message to the endpoint its [Http] declares: the client half of HttpSender. The
 * request's own DataAnnotations are checked before anything leaves, as SendPipeline does. */
export class Mediator {
  readonly #baseUrl: string;
  readonly #fetch: typeof fetch;
  readonly #headers: () => Record<string, string>;
  readonly #signIn: string | undefined;
  readonly #location: MediatorOptions["location"];
  #turned = false;
  #signedIn = false;

  constructor(options: MediatorOptions = {}) {
    this.#baseUrl = (options.baseUrl ?? "").replace(/\/+$/, "");
    this.#fetch = options.fetch ?? globalThis.fetch.bind(globalThis);
    this.#headers = options.headers ?? (() => ({}));
    this.#signIn = options.signIn;
    this.#location = options.location;
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
        // The session is the host's HttpOnly cookie: it travels to the page's own host and no
        // script reads it or holds a token in its place.
        credentials: "same-origin",
        signal: options.signal,
      });
    } catch (error) {
      if (error instanceof DOMException && error.name === "AbortError") {
        throw error;
      }

      throw new BusinessError(requestFailed(null, null));
    }

    let text: string;

    try {
      text = await response.text();
    } catch (error) {
      if (error instanceof DOMException && error.name === "AbortError") {
        throw error;
      }

      throw new BusinessError(requestFailed(response.status, "DeserializationFailed"));
    }

    if (!response.ok) {
      if (response.status === 401) {
        this.turnToSignIn();
      }

      throw new BusinessError(this.problemOf(response.status, text));
    }

    if (text.length === 0) {
      return undefined as TResult;
    }

    try {
      return JSON.parse(text) as TResult;
    } catch {
      throw new BusinessError(requestFailed(response.status, "DeserializationFailed"));
    }
  }

  /** What the client last read of its session. A 401 that meets a signed-in session is a
   * credential the server refused, and the turn to sign-in says so (SignInRoutes.StaleParameter):
   * the cookie went out with the 401, so the sign-in page has nothing else to learn it from.
   * StackProvider reports it from the session message it reads. */
  reportSession(isAuthenticated: boolean): void {
    this.#signedIn = isAuthenticated;
  }

  url<TRequest>(message: MessageType<TRequest, unknown>, request: TRequest): string {
    const values = request as Record<string, unknown>;
    const fields = Object.entries(message.fields) as [string, Field][];

    const path = message.path.replace(/{\*{0,2}([^{}:?=*]+)[^{}]*}/g, (_, token: string) => {
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

  // Only a 401: a 403 is a signed-in refusal, and turning it to sign-in would loop (NsNotAuthorized).
  // Once per page, however many sends answer 401 together: the turn leaves the page, and a second
  // one would carry the sign-in address as the way back.
  private turnToSignIn(): void {
    const location = this.#location ?? globalThis.location;

    if (this.#signIn === undefined || this.#turned || !location) {
      return;
    }

    this.#turned = true;

    const here = `${location.pathname}${location.search}${location.hash}`;
    const path = location.pathname.replace(/^\/+|\/+$/g, "");

    // The root is where a sign-in lands with no return address anyway, and the sign-in page
    // naming itself is a trip to nowhere — NsNotAuthorized's two guards.
    const carry = path.length > 0 && path.toLowerCase() !== this.#signIn.split(/[?#]/)[0].replace(/^\/+|\/+$/g, "").toLowerCase();
    const query: string[] = [];

    if (carry) {
      query.push(`${returnUrlParameter}=${encodeURIComponent(here)}`);
    }

    if (this.#signedIn) {
      query.push(`${staleParameter}=1`);
    }

    const separator = this.#signIn.includes("?") ? "&" : "?";

    location.assign(query.length > 0 ? `${this.#signIn}${separator}${query.join("&")}` : this.#signIn);
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
