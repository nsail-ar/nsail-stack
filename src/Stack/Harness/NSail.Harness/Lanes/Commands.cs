// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Harness.Lanes;

// A configured command as a process line: a script runs under its extension's interpreter, anything
// else is started as it is (found on the PATH), so a project needs no shell where it has none.
public static class Commands
{
    public static (string File, List<string> Args) Line(CommandOptions command, IEnumerable<string> extra, string bash, string powerShell)
    {
        var args = command.Args.Concat(extra).ToList();
        return Path.GetExtension(command.Run).ToLowerInvariant() switch
        {
            ".sh" => (bash, [command.Run, .. args]),
            ".ps1" => (powerShell, ["-NoProfile", "-NonInteractive", "-File", command.Run, .. args]),
            _ => (command.Run, args),
        };
    }

    public static Task<ShellResult> Run(
        CommandOptions command, IEnumerable<string> extra, HarnessOptions options, string workDir, IDictionary<string, string> env,
        TimeSpan timeout, TextWriter log, CancellationToken ct = default, bool lowPriority = false, Func<string, bool>? keep = null, string? runAs = null)
    {
        var (file, args) = Line(command, extra, options.Bash, options.PowerShell);
        return Shell.Run(file, args, workDir, env, timeout, log, ct: ct, lowPriority: lowPriority, runAs: runAs, keep: keep);
    }
}
