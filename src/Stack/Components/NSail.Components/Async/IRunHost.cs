// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components;

public interface IRunHost
{
    SurfaceContext? Surface { get; }

    object Source { get; }

    Task Report(ProblemEventArgs args);

    void StateChanged();
}
