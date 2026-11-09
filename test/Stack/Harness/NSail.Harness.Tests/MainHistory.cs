// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Harness.Lanes;
using NSail.Harness.Repositories;

namespace NSail.Harness.Tests;

// main as a real git repository: a red story's fix merged as Fix on top of Parent, the head a gate
// started on before the merge.
sealed class MainHistory : IDisposable
{
    public string Dir { get; } = Path.Combine(Path.GetTempPath(), "harness-main-" + Guid.NewGuid().ToString("N"));

    public string Parent { get; private set; } = "";

    public string Fix { get; private set; } = "";

    public Merge Merge => new(Fix, DateTimeOffset.UtcNow);

    public static async Task<MainHistory> Create()
    {
        var main = new MainHistory();
        Directory.CreateDirectory(main.Dir);
        await Shell.Git(main.Dir, "init", "-q", "-b", "main");
        main.Parent = await main.Commit("parent");
        main.Fix = await main.Commit("the fix");
        return main;
    }

    async Task<string> Commit(string message)
    {
        await Shell.Git(Dir, "-c", "user.name=t", "-c", "user.email=t@example.com", "commit", "-q", "--allow-empty", "-m", message);
        return (await Shell.Git(Dir, "rev-parse", "HEAD")).Trim();
    }

    public void Dispose()
    {
        if (!Directory.Exists(Dir)) return;
        foreach (var f in Directory.EnumerateFiles(Dir, "*", SearchOption.AllDirectories)) File.SetAttributes(f, FileAttributes.Normal);
        Directory.Delete(Dir, true);
    }
}
