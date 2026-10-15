// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

import { defaultClientConditions, defaultServerConditions } from "vite";
import { defineConfig } from "vitest/config";

// @nsail/stack from its sources in this workspace, as the Sample's client reads it: a test never
// waits on a dist/ that only a build writes.
export default defineConfig({
  resolve: {
    conditions: ["@nsail/source", ...defaultClientConditions],
  },
  ssr: {
    resolve: {
      conditions: ["@nsail/source", ...defaultServerConditions],
    },
  },
});
