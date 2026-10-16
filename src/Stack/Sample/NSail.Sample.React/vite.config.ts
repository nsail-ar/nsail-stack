// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

import { defineConfig, defaultClientConditions } from "vite";
import react from "@vitejs/plugin-react";

// The host serves the Blazor client at its root and this build under /react (ReactClient.Path on
// the .NET side), from react/ beside the host project. `npm run dev` serves the same app with hot
// reload and hands every /api call to a host running on its own http port.
export default defineConfig({
  base: "/react/",
  plugins: [react()],
  // The @nsail packages from their sources in this workspace, never a dist/ that may be stale or
  // missing: what an app outside the repository gets from npm is the built output.
  resolve: {
    conditions: ["@nsail/source", ...defaultClientConditions],
  },
  build: {
    outDir: "../NSail.Sample.Web/react",
    emptyOutDir: true,
  },
  server: {
    port: 4002,
    strictPort: true,
    proxy: {
      "/api": "http://localhost:4001",
    },
  },
});
