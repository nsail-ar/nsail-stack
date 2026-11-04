// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Diagnostics;
using NSail.Harness.Lanes;

namespace NSail.Harness.Tests;

public class ProcessGroupTests
{
    [Fact]
    public void ClosingTheGroupKillsTheProcessAndWhatItSpawned()
    {
        if (!OperatingSystem.IsWindows()) return;
        var job = ProcessGroup.Create();
        // A shell that starts a grandchild and reports its pid: the grandchild is what a killed harness used to orphan.
        var shell = Process.Start(new ProcessStartInfo("cmd.exe", "/c start /b ping -n 120 127.0.0.1 >nul & ping -n 120 127.0.0.1 >nul")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        })!;
        ProcessGroup.Join(job, shell);
        WaitFor(() => Children(shell).Skip(1).FirstOrDefault());
        var spawned = Children(shell).ToList();
        Assert.Equal(2, spawned.Count);

        ProcessGroup.Close(job);

        Assert.True(shell.WaitForExit(10000));
        Assert.All(spawned, p => Assert.True(p.WaitForExit(10000)));
    }

    static Process? WaitFor(Func<Process?> find)
    {
        for (var i = 0; i < 50; i++)
        {
            if (find() is { } p) return p;
            Thread.Sleep(100);
        }
        return null;
    }

    // The pings the shell started: none other runs on a build machine.
    static IEnumerable<Process> Children(Process shell)
    {
        return Process.GetProcessesByName("PING").Where(p => p.StartTime >= shell.StartTime);
    }
}
