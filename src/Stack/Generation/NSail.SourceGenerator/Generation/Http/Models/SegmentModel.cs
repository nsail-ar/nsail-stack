// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.SourceGenerator.Generation.Http.Models;

public enum SegmentType
{
    Literal,
    Parameter
}

public class SegmentModel
{
    public SegmentType Type { get; set; }

    public string? Value { get; set; }

    public override string ToString()
    {
        return $"{Value} ({Type})";
    }
}