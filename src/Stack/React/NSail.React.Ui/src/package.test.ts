// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack
/// <reference types="node" />

import { readdirSync, readFileSync } from "node:fs";
import { dirname, join, relative, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { describe, expect, it } from "vitest";

const pkg = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const root = resolve(pkg, "../../../..");
const vendor = /(?:from\s+|import\s*\(\s*|import\s+)["'](@mui\/|@emotion\/)/;

function sources(folder: string): string[] {
  return readdirSync(folder, { withFileTypes: true }).flatMap((entry) => {
    const path = join(folder, entry.name);

    if (entry.isDirectory()) {
      return entry.name === "node_modules" || entry.name === "dist" ? [] : sources(path);
    }

    return /\.(ts|tsx|js|jsx|mjs)$/.test(entry.name) ? [path] : [];
  });
}

// The React twin of PublicSurfaceHasNoVendorTypesTests: the vendor stays inside @nsail/ui, so a
// screen draws with the Ns pieces and a vendor change is this package's alone.
describe("the vendor", () => {
  it("is imported by @nsail/ui alone", () => {
    const workspaces: string[] = JSON.parse(readFileSync(join(root, "package.json"), "utf8")).workspaces;
    const offenders = workspaces
      .map((workspace) => resolve(root, workspace))
      .filter((folder) => folder !== pkg)
      .flatMap(sources)
      .filter((file) => vendor.test(readFileSync(file, "utf8")))
      .map((file) => relative(root, file));

    expect(workspaces.length).toBeGreaterThan(1);
    expect(offenders).toEqual([]);
  });

  it("is a dependency of @nsail/ui, and react stays the client's", () => {
    const manifest = JSON.parse(readFileSync(join(pkg, "package.json"), "utf8"));

    expect(Object.keys(manifest.dependencies)).toEqual(expect.arrayContaining(["@mui/material", "@emotion/react", "@emotion/styled"]));
    expect(Object.keys(manifest.peerDependencies)).toEqual(["react", "react-dom"]);
  });
});

// The pieces are styled by the theme alone: the package ships no stylesheet a client imports and
// declares no custom property of its own for one to override.
describe("the package", () => {
  it("exports no stylesheet", () => {
    const manifest = JSON.parse(readFileSync(join(pkg, "package.json"), "utf8"));

    expect(Object.keys(manifest.exports)).toEqual(["."]);
    expect(manifest.scripts.build).not.toMatch(/\.css/);
  });

  it("carries no CSS file and no --ns- variable", () => {
    const files = readdirSync(join(pkg, "src"));

    expect(files.filter((name) => name.endsWith(".css"))).toEqual([]);
    expect(sources(join(pkg, "src")).filter((file) => !file.endsWith(".test.ts") && /--ns-/.test(readFileSync(file, "utf8")))).toEqual([]);
  });
});
