// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

import type { EnumType, Issue, Problem } from "@nsail/messaging";
import mudBase from "../../NSail.Components.Mud/strings.json";
import mudEs from "../../NSail.Components.Mud/strings.es.json";

// The Stack's own sentences (Common.*, Actions.*, Problems.*) are the Blazor components'
// catalog, read from the very files NSail.Components.Mud embeds: one string in one file, whichever
// renderer draws it.
const stackCatalogs: Record<string, Record<string, string>> = { en: mudBase, es: { ...mudBase, ...mudEs } };

export function stackStrings(language: string): Record<string, string> {
  return stackCatalogs[language] ?? stackCatalogs.en;
}

const token = /{([A-Za-z0-9_]+)}/g;

function fill(text: string, args?: Record<string, string> | null): string {
  if (!args) {
    return text;
  }

  return text.replace(token, (whole, name: string) => (name in args ? args[name] : whole));
}

/** StringManager, for the browser: a missing key renders as the key itself (or the fallback
 * given), so an untranslated string is visible instead of guessed. */
export class Strings {
  readonly language: string;
  readonly #entries: Record<string, string>;

  constructor(language: string, entries: Record<string, string>) {
    this.language = language;
    this.#entries = entries;
  }

  translate(key: string, fallback?: string): string {
    return this.#entries[key] ?? fallback ?? key;
  }

  /** Named ({name}) or positional ({0}) tokens filled from the arguments. */
  format(key: string, args: Record<string, string> | readonly string[]): string {
    const named = Array.isArray(args) ? Object.fromEntries(args.map((value, index) => [String(index), value])) : (args as Record<string, string>);

    return fill(this.translate(key), named);
  }

  count(key: string, count: number): string {
    const plural = count !== 1 ? this.#entries[`${key}.Plural`] : undefined;

    return fill(plural ?? this.translate(key), { "0": String(count), count: String(count) });
  }

  enumValue(type: EnumType, value: string | null | undefined): string {
    if (value === null || value === undefined) {
      return "";
    }

    return this.translate(`${type.key}.${value}`, value);
  }

  /** "Problems.{Code}.{Source}", then "Problems.{Code}", then the message the sender wrote; the
   * entity argument is a key and resolves to its label first. */
  issue(issue: Issue): string {
    const scoped = issue.source ? this.#entries[`Problems.${issue.code}.${issue.source}`] : undefined;
    const text = scoped ?? this.#entries[`Problems.${issue.code}`] ?? issue.message;
    const args = issue.arguments ? { ...issue.arguments } : null;

    if (args?.entity) {
      args.entity = this.translate(args.entity, args.entity.slice(args.entity.lastIndexOf(".") + 1));
    }

    return fill(text, args);
  }

  problem(problem: Problem): string {
    return this.translate(`Problems.${problem.code}.Title`, problem.title);
  }
}
