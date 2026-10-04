// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

// The host serves the Blazor client at its root and this build under /react (ReactClient.Path on
// the .NET side), from react/ beside the host project. `npm run dev` serves the same app with hot
// reload and hands every /api call to a host running on its own http port.
export default defineConfig({
  base: "/react/",
  plugins: [react()],
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
