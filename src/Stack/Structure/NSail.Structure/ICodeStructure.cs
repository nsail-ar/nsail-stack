// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.Structure;

public interface ICodeStructure
{
    TypePlacement GetPlacement(Type type);
}