// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

// The route table: each address is written here once, and a link asks for it by name — a path
// literal anywhere else is a second copy that can drift.
function route<TParams extends Record<string, string> | void = void>(pattern: string) {
  return {
    pattern,
    href(params: TParams): string {
      if (!params) {
        return pattern;
      }

      return pattern.replace(/:([A-Za-z]+)/g, (_, name: string) => encodeURIComponent((params as Record<string, string>)[name]));
    },
  };
}

export const routes = {
  contacts: route("/"),
  newContact: route("/contacts/new"),
  contact: route<{ id: string }>("/contacts/:id"),
};
