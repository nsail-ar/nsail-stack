// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Problems;

public static class InputProblem
{
    public static InputProblemBuilder<T> For<T>()
    {
        return new();
    }

    public static Problem InvalidModel(params Issue[] issues)
    {
        return new(
            code: "InvalidModel",
            title: "One or more fields contain invalid data",
            issues: issues,
            status: 400);
    }

    public static InputProblemBuilder<TModel> InvalidModel<TModel>()
    {
        return new();
    }
}
