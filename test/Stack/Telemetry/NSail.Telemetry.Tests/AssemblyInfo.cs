// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

// An ActivityListener is process-wide and the credential is an environment variable: two
// classes running at once would each record the other's spans and race each other's variable.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
