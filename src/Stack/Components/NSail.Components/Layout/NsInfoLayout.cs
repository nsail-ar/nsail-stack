// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components;

/// <summary>How an NsInfoPanel sets its facts: Facts is label over value in columns that wrap,
/// List is one fact per row, its label at the start and its value at the end.</summary>
public enum NsInfoLayout
{
    Facts,
    List
}
