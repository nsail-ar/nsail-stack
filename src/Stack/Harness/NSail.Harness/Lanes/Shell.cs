// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Diagnostics;
using System.Text;

namespace NSail.Harness.Lanes;

// A process with its own environment and working folder; the whole output, stdout and
// stderr, goes to the log as it comes and its last TailChars characters back to the caller,
// preceded by the lines of the cut-off head that `keep` accepts.
public static class Shell
{
    // What the harness's own git (clone, fetch, push) authenticates with: set once at startup, never
    // written to the process environment, so no child but a `git` it starts reads it.
    public static IReadOnlyDictionary<string, string> GitEnvironment { get; set; } = new Dictionary<string, string>();

    public const int TailChars = 1 << 20;

    // Lines a caller's `keep` saves from the cut-off head, so what it reads (failures, quota signals) survives the tail.
    public const int KeptLines = 20_000;

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

    // The names that say whose account a process runs in: under `runAs` the target's own sudo sets them.
    static readonly string[] Identity = ["HOME", "USER", "USERNAME", "LOGNAME", "SHELL", "XDG_RUNTIME_DIR", "XDG_CONFIG_HOME", "XDG_CACHE_HOME", "XDG_DATA_HOME"];

    public static async Task<ShellResult> Run(
        string file, IEnumerable<string> args, string workDir, IDictionary<string, string>? env = null,
        TimeSpan? timeout = null, TextWriter? log = null, string? stdin = null, CancellationToken ct = default, bool lowPriority = false, string? runAs = null, int tailChars = TailChars, Func<string, bool>? keep = null, bool stdoutOnly = false)
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

        var seat = false;
        if (runAs is not null && !OperatingSystem.IsWindows())
        {
            // The process runs as another user, never with this one's home or keys: sudo (no prompt,
            // the target's own HOME) carries only the names this method set. The seat gets its own
            // session, which a timeout kills through sudo because this user cannot signal the seat.
            seat = true;
            foreach (var name in Identity) psi.Environment.Remove(name);
            var inner = psi.ArgumentList.ToList();
            psi.ArgumentList.Clear();
            string[] head = ["-n", "-H", "-u", runAs, $"--preserve-env={string.Join(',', psi.Environment.Keys)}", "--", "setsid", "-w"];
            foreach (var a in head) psi.ArgumentList.Add(a);
            // Priority cannot be set on a process this user does not own: it rides inside the line.
            if (lowPriority)
            {
                foreach (var a in new[] { "nice", "-n", "10" }) psi.ArgumentList.Add(a);
            }
            psi.ArgumentList.Add(file);
            foreach (var a in inner) psi.ArgumentList.Add(a);
            psi.FileName = "sudo";
            lowPriority = false;
        }

        return await Execute(psi, stdin, timeout, log, ct, lowPriority, seat ? runAs : null, tailChars, keep, stdoutOnly);
    }

    static async Task<ShellResult> Execute(ProcessStartInfo psi, string? stdin, TimeSpan? timeout, TextWriter? log, CancellationToken ct, bool lowPriority, string? runAs, int tailChars, Func<string, bool>? keep, bool stdoutOnly)
    {
        using var p = new Process { StartInfo = psi, EnableRaisingEvents = true };
        var tail = new Queue<string>();
        var tailLength = 0;
        var kept = new List<string>();
        var errors = new List<string>();
        var gate = new object();
        void Line(string? l, bool toTail = true)
        {
            if (l is null) return;
            lock (gate)
            {
                if (!toTail)
                {
                    if (errors.Count < 50) errors.Add(l);
                    log?.WriteLine(l);
                    return;
                }
                tail.Enqueue(l);
                tailLength += l.Length + Environment.NewLine.Length;
                while (tailLength > tailChars && tail.Count > 1)
                {
                    var evicted = tail.Dequeue();
                    tailLength -= evicted.Length + Environment.NewLine.Length;
                    if (keep is not null && kept.Count < KeptLines && keep(evicted)) kept.Add(evicted);
                }
                log?.WriteLine(l);
            }
        }
        p.OutputDataReceived += (_, e) => Line(e.Data);
        p.ErrorDataReceived += (_, e) => Line(e.Data, !stdoutOnly);
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
        if (stdin is not null) _ = WriteStdin(p, stdin);
        var timedOut = false;
        try
        {
            await exited.Task.WaitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            timedOut = !ct.IsCancellationRequested;
            var sessions = runAs is null ? [] : SeatSessions(p.Id);
            Kill(p);
            if (runAs is not null) await KillSeat(runAs, sessions);
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
            foreach (var l in kept) output.AppendLine(l);
            foreach (var l in tail) output.AppendLine(l);
            return new ShellResult(timedOut ? -1 : p.ExitCode, output.ToString(), timedOut, string.Join(Environment.NewLine, errors));
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
        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception or AggregateException)
        {
        }
    }

    // The sessions below sudo's process other than its own: the harness reads them from /proc, where
    // the seat cannot write, so nothing the seat controls decides what gets killed.
    static List<int> SeatSessions(int root)
    {
        var parents = new Dictionary<int, int>();
        var sessions = new Dictionary<int, int>();
        try
        {
            foreach (var dir in Directory.EnumerateDirectories("/proc"))
            {
                if (!int.TryParse(Path.GetFileName(dir), out var pid)) continue;
                try
                {
                    var stat = File.ReadAllText(Path.Combine(dir, "stat"));
                    var fields = stat[(stat.LastIndexOf(')') + 2)..].Split(' ');
                    parents[pid] = int.Parse(fields[1], System.Globalization.CultureInfo.InvariantCulture);
                    sessions[pid] = int.Parse(fields[3], System.Globalization.CultureInfo.InvariantCulture);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException or FormatException or ArgumentOutOfRangeException or IndexOutOfRangeException)
                {
                }
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return [];
        }

        var own = sessions.GetValueOrDefault(root);
        var found = new List<int>();
        var queue = new Queue<int>([root]);
        var seen = new HashSet<int> { root };
        while (queue.Count > 0)
        {
            var parent = queue.Dequeue();
            foreach (var (pid, ppid) in parents)
            {
                if (ppid != parent || !seen.Add(pid)) continue;
                queue.Enqueue(pid);
                var sid = sessions[pid];
                if (sid > 1 && sid != own && !found.Contains(sid)) found.Add(sid);
            }
        }
        return found;
    }

    // The seat's whole sessions, killed as the seat's own user: sudo's rule for the account must allow `pkill`.
    static async Task KillSeat(string runAs, List<int> sessions)
    {
        foreach (var sid in sessions)
        {
            try
            {
                using var kill = Process.Start(new ProcessStartInfo("sudo", ["-n", "-u", runAs, "pkill", "-KILL", "-s", sid.ToString(System.Globalization.CultureInfo.InvariantCulture)]) { UseShellExecute = false });
                if (kill is not null) await kill.WaitForExitAsync(new CancellationTokenSource(TimeSpan.FromSeconds(15)).Token);
            }
            catch (Exception e) when (e is System.ComponentModel.Win32Exception or OperationCanceledException)
            {
            }
        }
    }

    // A git command whose output the harness parses: stderr (a warning git chooses to print) is never part of what it reads.
    public static async Task<string> GitOut(string workDir, params string[] args)
    {
        var r = await Run("git", args, workDir, new Dictionary<string, string> { ["GIT_TERMINAL_PROMPT"] = "0" }, TimeSpan.FromMinutes(10), stdoutOnly: true);
        if (r.TimedOut) throw new InvalidOperationException($"git {string.Join(' ', args)}: timed out");
        if (r.ExitCode != 0) throw new InvalidOperationException($"git {string.Join(' ', args)} exited {r.ExitCode}: {r.Error.Trim()}");
        return r.Output.Trim();
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
