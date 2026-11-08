// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Harness.Jobs;

namespace NSail.Harness.Tests;

public class DevJobScreenTests
{
    static readonly string[] Strict = [".github/workflows/**"];

    static HarnessOptions Options(bool allow = false)
    {
        return new HarnessOptions { ProtectedPaths = [.. Strict], AllowProtectedPaths = allow };
    }

    [Fact]
    public async Task TheScreenIsStrictByDefault()
    {
        var board = new MemoryBoard();
        board.Add(1);

        Assert.Equal(Strict, await DevJob.ScreenOf(Options(), board, 1));
    }

    [Fact]
    public async Task TheOptionLiftsTheScreen()
    {
        var board = new MemoryBoard();
        board.Add(1);

        Assert.Empty(await DevJob.ScreenOf(Options(allow: true), board, 1));
    }

    [Fact]
    public async Task TheLabelLiftsTheScreen()
    {
        var board = new MemoryBoard();
        board.Add(1, labels: Labels.AllowProtected);

        Assert.Empty(await DevJob.ScreenOf(Options(), board, 1));
    }

    [Fact]
    public async Task ABoardThatThrowsScreensStrictly()
    {
        Assert.Equal(Strict, await DevJob.ScreenOf(Options(), new MemoryBoard(), 1));
    }
}
