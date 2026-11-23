// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Harness;

// The board a pass planned from: its open stories per stage, every stage of the pipeline counted,
// and the held ones, an entry per hold.
public sealed record BoardView(string Repository, DateTimeOffset ReadAt, IReadOnlyList<StageCount> Stages, IReadOnlyList<HeldStory> Held);

public sealed record StageCount(string Stage, int Open);

// Hold: the label that holds the story (`ruling`, `stalled`, `unwalkable`), or `gate` when its PR's gate is red.
public sealed record HeldStory(int Story, string Title, string Stage, string Hold);
