// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Diagnostics;

namespace NSail.Harness.Tests;

// Writes a script a test then runs. On Unix the test process never opens the script itself for
// writing: a process forked by a parallel test inherits every open descriptor until it execs, and an
// exec of a file someone holds open for writing fails with "Text file busy". The text goes to a
// sibling file and `install` copies it in place from a process of its own, which forks nothing.
static class Executable
{
    public static void Write(string path, string script)
    {
        if (OperatingSystem.IsWindows())
        {
            File.WriteAllText(path, script);
            return;
        }
        var text = path + ".txt";
        File.WriteAllText(text, script);
        using var install = Process.Start(new ProcessStartInfo("install", ["-m", "700", text, path]) { UseShellExecute = false })!;
        install.WaitForExit();
        File.Delete(text);
        if (install.ExitCode != 0) throw new InvalidOperationException($"install could not write {path}");
    }
}
