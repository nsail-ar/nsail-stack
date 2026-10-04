// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using NSail.Problems;

namespace NSail.Components;

public sealed class ProblemEventArgs : EventArgs
{
    public ProblemEventArgs(Problem problem, object? source = null, Exception? exception = null)
    {
        Problem = problem;
        Source = source;
        Exception = exception;
    }

    public Problem Problem { get; }

    public object? Source { get; }

    public Exception? Exception { get; }

    public bool Handled { get; set; }
}