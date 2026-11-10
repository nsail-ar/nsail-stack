// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Harness.Planning;

// A product whose cut failed and has not shipped: the release takes the whole harness until it does.
// Story is the product's red story, Since when it was filed (null when the board does not say) and
// Waits what the release waits for now.
public sealed record Andon(string Product, int Story, DateTimeOffset? Since, string Waits);
