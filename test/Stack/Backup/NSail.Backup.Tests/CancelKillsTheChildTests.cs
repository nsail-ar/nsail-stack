// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Diagnostics;

namespace NSail.Backup.Tests;

public sealed class CancelKillsTheChildTests
{
    [FactOrSkipOnWindows]
    public async Task ACancelKillsTheChildProcess()
    {
        var pidFile = Path.Combine(Path.GetTempPath(), $"nsail-cancel-{Guid.NewGuid():N}.pid");

        try
        {
            using var cancel = new CancellationTokenSource();
            var running = Postgres.Run("sh", ["-c", $"echo $$ > '{pidFile}'; exec sleep 30"], null, cancel.Token);

            var deadline = DateTime.UtcNow.AddSeconds(20);

            while (!(File.Exists(pidFile) && File.ReadAllText(pidFile).Trim().Length > 0))
            {
                Assert.True(DateTime.UtcNow < deadline, "the child never started");
                Assert.False(running.IsCompleted, "the child ended before the cancel");

                await Task.Delay(20);
            }

            using var child = Process.GetProcessById(int.Parse(File.ReadAllText(pidFile).Trim()));

            await cancel.CancelAsync();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running);

            Assert.True(child.WaitForExit(TimeSpan.FromSeconds(10)), "the child outlived the cancel");
        }
        finally
        {
            File.Delete(pidFile);
        }
    }
}

sealed class FactOrSkipOnWindowsAttribute : FactAttribute
{
    public FactOrSkipOnWindowsAttribute()
    {
        if (OperatingSystem.IsWindows())
        {
            Skip = "needs sh and sleep";
        }
    }
}
