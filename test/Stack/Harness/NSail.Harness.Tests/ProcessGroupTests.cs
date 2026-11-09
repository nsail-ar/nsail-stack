// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Diagnostics;
using NSail.Harness.Lanes;

namespace NSail.Harness.Tests;

public class ProcessGroupTests
{
    [FactOn("windows")]
    public void ClosingTheGroupKillsTheProcessAndWhatItSpawned()
    {
        var job = ProcessGroup.Create();
        // A shell that starts two grandchildren and reports their pids: the grandchildren are what a
        // killed harness used to orphan, and only those two are looked at, never a name. Their own
        // output goes to files, so the only lines on the test's pipe are the pids.
        var shell = Process.Start(new ProcessStartInfo("powershell.exe",
            "-NoProfile -NonInteractive -Command \"$a = Start-Process ping -ArgumentList '-n','120','127.0.0.1' -NoNewWindow -PassThru -RedirectStandardOutput ([IO.Path]::GetTempFileName()); $b = Start-Process ping -ArgumentList '-n','120','127.0.0.1' -NoNewWindow -PassThru -RedirectStandardOutput ([IO.Path]::GetTempFileName()); $a.Id; $b.Id; Wait-Process -Id $a.Id,$b.Id\"")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
        })!;
        ProcessGroup.Join(job, shell);
        var spawned = new[] { Pid(shell), Pid(shell) };

        ProcessGroup.Close(job);

        Assert.True(shell.WaitForExit(10000));
        Assert.All(spawned, pid => Assert.True(Gone(pid)));
    }

    [FactOn("linux")]
    public async Task ATimedOutShellTakesItsGrandchildWithItOnLinux()
    {
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

    // The next line on the shell's pipe that is a number: a stray line is passed over, never parsed.
    static int Pid(Process shell)
    {
        while (shell.StandardOutput.ReadLine() is { } line)
        {
            if (int.TryParse(line.Trim(), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var pid)) return pid;
        }
        throw new InvalidOperationException("the shell exited before it reported both pids");
    }
}
