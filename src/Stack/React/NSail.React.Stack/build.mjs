// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

// The package as npm ships it. The sources read the Stack's own sentences from the catalog
// NSail.Components.Mud embeds, by a path that leaves the package: the published build carries a
// copy of those files and points its import at the copy, so the two renderers still say one
// sentence per key and the tarball needs nothing outside itself.
import { execSync } from "node:child_process";
import { copyFileSync, mkdirSync, readFileSync, writeFileSync } from "node:fs";

const source = "../../../Components/NSail.Components.Mud/";
const copied = "./catalogs/";
const catalogs = ["strings.json", "strings.es.json"];

execSync("tsc -p tsconfig.build.json", { stdio: "inherit" });

mkdirSync("dist/catalogs", { recursive: true });

for (const name of catalogs) {
  copyFileSync(`../../Components/NSail.Components.Mud/${name}`, `dist/catalogs/${name}`);
}

const strings = readFileSync("dist/strings.js", "utf8");
const pointed = strings.replaceAll(source, copied);

if (pointed === strings) {
  throw new Error(`dist/strings.js no longer imports ${source}: the catalogs are not where this build looks.`);
}

writeFileSync("dist/strings.js", pointed);
