// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Diagnostics;
using System.Text;

namespace NSail.Harness.Lanes;

// A process with its own environment and working folder; the whole output, stdout and
// stderr, goes to the log as it comes and its last TailChars characters back to the caller.
public static class Shell
{
    // What the harness's own git (clone, fetch, push) authenticates with: set once at startup, never
    // written to the process environment, so no child but a `git` it starts reads it.
    public static IReadOnlyDictionary<string, string> GitEnvironment { get; set; } = new Dictionary<string, string>();

    public const int TailChars = 1 << 20;

    static readonly string[] InheritedNames =
    [
        "PATH", "PATHEXT", "HOME", "USER", "USERNAME", "LOGNAME", "SHELL", "TERM", "LANG", "TZ",
        "TMP", "TEMP", "TMPDIR", "USERPROFILE", "HOMEDRIVE", "HOMEPATH", "APPDATA", "LOCALAPPDATA", "PROGRAMDATA",
        "PROGRAMFILES", "PROGRAMFILES(X86)", "PROGRAMW6432", "COMMONPROGRAMFILES", "COMMONPROGRAMFILES(X86)", "COMMONPROGRAMW6432",
        "SYSTEMROOT", "SYSTEMDRIVE", "WINDIR", "COMSPEC", "OS", "NUMBER_OF_PROCESSORS", "PROCESSOR_ARCHITECTURE",
        "XDG_RUNTIME_DIR", "XDG_CONFIG_HOME", "XDG_CACHE_HOME", "XDG_DATA_HOME", "DISPLAY", "WSL_DISTRO_NAME",
        "DOTNET_ROOT", "DOTNET_ROOT(X86)", "NUGET_PACKAGES", "NUGET_HTTP_CACHE_PATH", "NUGET_PLUGINS_CACHE_PATH", "CARGO_HOME", "NVM_DIR",
    ];

    // The tools' own names a child inherits — a machine's PATH, home, temp and locale, never a
    // credential: whatever else a process needs it is given by name (`env`, the lane's variables).
    public static IEnumerable<string> Inherited(IEnumerable<string> names)
    {
        return names.Where(n => InheritedNames.Contains(n, StringComparer.OrdinalIgnoreCase)
            || n.StartsWith("LC_", StringComparison.Ordinal));
    }

    // A seat shares the repository's common git directory, so what it plants there (a hook, a
    // fsmonitor command) must never run in the harness's git: command-line config outranks the repository's.
    static readonly string[] Hardened = ["-c", $"core.hooksPath={(OperatingSystem.IsWindows() ? "NUL" : "/dev/null")}", "-c", "core.fsmonitor=false", "-c", "protocol.ext.allow=never"];

    public static async Task<ShellResult> Run(
        string file, IEnumerable<string> args, string workDir, IDictionary<string, string>? env = null,
        TimeSpan? timeout = null, TextWriter? log = null, string? stdin = null, CancellationToken ct = default, bool lowPriority = false, int tailChars = TailChars)
    {
        var psi = new ProcessStartInfo(file)
        {
            WorkingDirectory = workDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = stdin is not null,
            UseShellExecute = false,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        if (file == "git")
        {
            foreach (var a in Hardened) psi.ArgumentList.Add(a);
        }
        foreach (var a in args) psi.ArgumentList.Add(a);
        psi.Environment.Clear();
        foreach (var name in Inherited(Environment.GetEnvironmentVariables().Keys.Cast<string>()))
        {
            psi.Environment[name] = Environment.GetEnvironmentVariable(name);
        }
        if (file == "git")
        {
            foreach (var (k, v) in GitEnvironment) psi.Environment[k] = v;
        }
        if (env is not null)
        {
            foreach (var (k, v) in env) psi.Environment[k] = v;
        }

        using var p = new Process { StartInfo = psi, EnableRaisingEvents = true };
        var tail = new Queue<string>();
        var tailLength = 0;
        var gate = new object();
        void Line(string? l)
        {
            if (l is null) return;
            lock (gate)
            {
                tail.Enqueue(l);
                tailLength += l.Length + Environment.NewLine.Length;
                while (tailLength > tailChars && tail.Count > 1)
                {
                    tailLength -= tail.Dequeue().Length + Environment.NewLine.Length;
                }
                log?.WriteLine(l);
            }
        }
        p.OutputDataReceived += (_, e) => Line(e.Data);
        p.ErrorDataReceived += (_, e) => Line(e.Data);
        var exited = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        p.Exited += (_, _) => exited.TrySetResult();
        p.Start();
        try
        {
            ProcessGroup.Join(p);
        }
        catch
        {
            Kill(p);
            throw;
        }
        if (lowPriority)
        {
            // Children inherit it, on Windows and Linux alike: a lane's builds yield to whoever sits at the machine.
            try
            {
                p.PriorityClass = ProcessPriorityClass.BelowNormal;
            }
            catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception or PlatformNotSupportedException)
            {
            }
        }
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        if (timeout is { } t) cts.CancelAfter(t);
        // A child that never reads stdin blocks the write: it runs inside the timeout window, and the kill breaks its pipe.
        var written = stdin is null ? Task.CompletedTask : WriteStdin(p, stdin);
        var timedOut = false;
        try
        {
            await exited.Task.WaitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            timedOut = !ct.IsCancellationRequested;
            Kill(p);
            await exited.Task.WaitAsync(TimeSpan.FromSeconds(30), CancellationToken.None).ContinueWith(_ => { }, TaskScheduler.Default);
            if (!timedOut) throw;
        }

        // The process has exited, but a child it left behind (an app host, a test host) can hold the
        // output pipe open for good: the output gets a bounded drain, never an unbounded wait.
        using var drain = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        try
        {
            await p.WaitForExitAsync(drain.Token);
        }
        catch (OperationCanceledException)
        {
            p.CancelOutputRead();
            p.CancelErrorRead();
        }
        lock (gate)
        {
            log?.Flush();
            var output = new StringBuilder();
            foreach (var l in tail) output.AppendLine(l);
            return new ShellResult(timedOut ? -1 : p.ExitCode, output.ToString(), timedOut);
        }
    }

    static async Task WriteStdin(Process p, string stdin)
    {
        try
        {
            await p.StandardInput.WriteAsync(stdin);
            p.StandardInput.Close();
        }
        catch (Exception e) when (e is IOException or ObjectDisposedException or InvalidOperationException)
        {
        }
    }

    static void Kill(Process p)
    {
        try
        {
            p.Kill(entireProcessTree: true);
        }
        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
        }
    }

    // No git call may hang the harness: a credential helper waiting on a window, a network
    // that never answers. Ten minutes is past any fetch or clone this repository takes.
    public static async Task<string> Git(string workDir, params string[] args)
    {
        var r = await Run("git", args, workDir, new Dictionary<string, string> { ["GIT_TERMINAL_PROMPT"] = "0" }, TimeSpan.FromMinutes(10));
        if (r.TimedOut) throw new InvalidOperationException($"git {string.Join(' ', args)}: timed out");
        if (r.ExitCode != 0) throw new InvalidOperationException($"git {string.Join(' ', args)}: {r.Output.Trim()}");
        return r.Output.Trim();
    }
}
