// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.Configuration;

namespace NSail.Harness.Board;

// A tracker the configuration names (BoardOptions.Provider): it opens a board from that board's own
// section of the configuration, where its provider's settings sit beside the harness's.
public interface IBoardProvider
{
    string Name { get; }

    IBoard Open(IConfigurationSection board);
}
