// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Diagnostics;
using System.Text;

namespace NSail.Harness.Lanes;

// A process with its own environment and working folder; the whole output, stdout and
// stderr, goes to the log as it comes and back to the caller.
public static class Shell
{
    public static async Task<ShellResult> Run(
        string file, IEnumerable<string> args, string workDir, IDictionary<string, string>? env = null,
        TimeSpan? timeout = null, TextWriter? log = null, string? stdin = null, CancellationToken ct = default, bool lowPriority = false)
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
        foreach (var a in args) psi.ArgumentList.Add(a);
        if (env is not null)
        {
            foreach (var (k, v) in env) psi.Environment[k] = v;
        }

        using var p = new Process { StartInfo = psi, EnableRaisingEvents = true };
        var output = new StringBuilder();
        var gate = new object();
        void Line(string? l)
        {
            if (l is null) return;
            lock (gate)
            {
                output.AppendLine(l);
                log?.WriteLine(l);
            }
        }
        p.OutputDataReceived += (_, e) => Line(e.Data);
        p.ErrorDataReceived += (_, e) => Line(e.Data);
        var exited = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        p.Exited += (_, _) => exited.TrySetResult();
        p.Start();
        ProcessGroup.Join(p);
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
        if (stdin is not null)
        {
            await p.StandardInput.WriteAsync(stdin);
            p.StandardInput.Close();
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        if (timeout is { } t) cts.CancelAfter(t);
        var timedOut = false;
        try
        {
            await exited.Task.WaitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            timedOut = !ct.IsCancellationRequested;
            try
            {
                p.Kill(entireProcessTree: true);
            }
            catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
            }
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
            return new ShellResult(timedOut ? -1 : p.ExitCode, output.ToString(), timedOut);
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
