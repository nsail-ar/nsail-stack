// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components.Tests;

public sealed class SurfaceQueryTests
{
    [Fact]
    public void Undefined_enum_values_are_ignored()
    {
        Assert.Equal(default, SurfaceQuery.GetValue<QueryState>("?s=99", "s"));
        Assert.Equal(QueryState.Closed, SurfaceQuery.GetValue<QueryState>("?s=closed", "s"));
        Assert.Equal(default, SurfaceQuery.GetValue<QueryState>("?s=Open,Closed", "s"));
        Assert.Equal(QueryFlags.A | QueryFlags.B, SurfaceQuery.GetValue<QueryFlags>("?f=A,B", "f"));
        Assert.Equal(default, SurfaceQuery.GetValue<QueryFlags>("?f=16", "f"));
    }

    [Fact]
    public void A_negative_value_never_throws()
    {
        Assert.Equal(default, SurfaceQuery.GetValue<QueryFlags>("?f=-1", "f"));
        Assert.Equal(PermFlags.Read | PermFlags.Write, SurfaceQuery.GetValue<PermFlags>("?p=3", "p"));
        Assert.Equal(PermFlags.All, SurfaceQuery.GetValue<PermFlags>("?p=-1", "p"));
    }

    enum QueryState { Open = 1, Closed = 2 }

    [Flags]
    enum QueryFlags { A = 1, B = 2 }

    [Flags]
    enum PermFlags { None = 0, Read = 1, Write = 2, All = ~0 }
}
