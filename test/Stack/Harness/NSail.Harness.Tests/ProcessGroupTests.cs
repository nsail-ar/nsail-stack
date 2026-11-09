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

    [Fact]
    public async Task ATimedOutShellTakesItsGrandchildWithItOnLinux()
    {
        if (OperatingSystem.IsWindows()) return;
        // The shell reports the pid of the sleeper it started: only that process is looked at, never a name.
        var result = await Shell.Run("sh", ["-c", "sleep 300 & echo $!; wait"], Path.GetTempPath(), timeout: TimeSpan.FromSeconds(2));

        Assert.True(result.TimedOut);
        var pid = int.Parse(result.Output.Trim().Split('\n')[0], System.Globalization.CultureInfo.InvariantCulture);
        Assert.True(Gone(pid));
    }

    static bool Gone(int pid)
    {
        for (var i = 0; i < 50; i++)
        {
            try
            {
                using var p = Process.GetProcessById(pid);
                if (p.HasExited) return true;
            }
            catch (ArgumentException)
            {
                return true;
            }
            Thread.Sleep(100);
        }
        return false;
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
