// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

// The drain's meter is the process's, so two classes delivering at once would add to each
// other's readings and the count an alert is written against could not be asserted at all.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
