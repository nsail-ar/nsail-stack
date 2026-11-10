// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Text;

namespace NSail.Harness.Lanes;

// One bare clone per machine and a fixed set of worktree slots on it. A slot is reused
// job after job and only reset, never wiped: what git ignores (bin, obj) stays warm for the
// next build, the way a runner's `clean: false` checkout did.
public sealed class Workspace
{
    readonly string _root;
    readonly string _repoUrl;
    readonly string? _credential;
    readonly string _name;
    readonly string _email;
    readonly string _branch;
    readonly SemaphoreSlim _git = new(1);
    readonly Queue<int> _free;

    // The clone reaches its remote through this credential helper (none: the remote is open), and the
    // commits the harness makes in it carry this identity.
    public Workspace(string root, string cloneUrl, string? credentialHelper, int slots, string commitName, string commitEmail, string defaultBranch = "main")
    {
        _root = Path.GetFullPath(root);
        _repoUrl = cloneUrl;
        _credential = credentialHelper;
        _name = commitName;
        _email = commitEmail;
        _branch = defaultBranch;
        _free = new Queue<int>(Enumerable.Range(1, slots));
        Directory.CreateDirectory(_root);
    }

    string Bare => Path.Combine(_root, "repo.git");

    // The remote default branch every slot starts from.
    public string Tip => $"origin/{_branch}";

    // The remote by its address, never by the name `origin` the shared config resolves: the harness's
    // network git goes where it was configured to, whatever a seat wrote in the common git directory.
    public string Url => _repoUrl;

    // Prefix of a git that reaches the remote: the helper list is the harness's own alone.
    public string[] Guard => ["-c", "credential.helper=", .. Helper()];

    string[] Helper()
    {
        return _credential is null ? [] : ["-c", $"credential.helper={_credential}"];
    }

    public string SlotPath(int slot)
    {
        return Path.Combine(_root, $"slot-{slot}");
    }

    public int? Take()
    {
        lock (_free)
        {
            return _free.Count > 0 ? _free.Dequeue() : null;
        }
    }

    public void Give(int slot)
    {
        lock (_free)
        {
            _free.Enqueue(slot);
        }
    }

    // A slot at the default branch's tip, clean of everything git tracks or does not ignore.
    public Task<string> Prepare(int slot)
    {
        return PrepareAt(SlotPath(slot));
    }

    // The doctrine a reviewer judges against: the default branch's tree beside the PR head it reads.
    public Task<string> PrepareDoctrine(int slot)
    {
        return PrepareAt(Path.Combine(_root, $"doctrine-{slot}"));
    }

    // The clone's configuration and hooks are the harness's alone: a seat shares the common git
    // directory, so whatever it planted there is gone before the harness runs git in it again.
    public async Task Restore()
    {
        if (!Directory.Exists(Bare)) return;
        await _git.WaitAsync();
        try
        {
            await WriteConfig();
        }
        finally
        {
            _git.Release();
        }
    }

    // What the platform decides about its filesystem, measured once on a throwaway clone: the
    // shared config is the seat's to write, so nothing is read back from it.
    string[]? _platform;

    async Task<string[]> Platform()
    {
        if (_platform is not null) return _platform;
        var probe = Path.Combine(_root, "probe.git");
        if (Directory.Exists(probe)) Directory.Delete(probe, recursive: true);
        await Shell.Git(_root, "init", "-q", "--bare", probe);
        var lines = new List<string>();
        foreach (var key in new[] { "core.filemode", "core.symlinks", "core.ignorecase", "core.precomposeunicode" })
        {
            var r = await Shell.Run("git", ["config", "--file", Path.Combine(probe, "config"), "--get", key], _root);
            if (r.ExitCode == 0) lines.Add($"\t{key["core.".Length..]} = {r.Output.Trim()}");
        }
        Directory.Delete(probe, recursive: true);
        return _platform = [.. lines];
    }

    static string Quote(string value)
    {
        return "\"" + value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
    }

    // Caller holds _git. The file is written whole, never edited.
    async Task WriteConfig()
    {
        var config = Path.Combine(Bare, "config");
        var text = new StringBuilder();
        text.Append("[core]\n\trepositoryformatversion = 0\n\tbare = true\n");
        foreach (var line in await Platform()) text.Append(line).Append('\n');
        // A slot checks files out as the repository holds them (LF), as on Linux: a CRLF checkout
        // makes a test reading a file or a template see bytes CI never sees.
        text.Append("\tautocrlf = false\n\teol = lf\n");
        text.Append($"[remote \"origin\"]\n\turl = {Quote(_repoUrl)}\n\tfetch = +refs/heads/*:refs/remotes/origin/*\n");
        // The empty helper first resets the list: a machine-wide helper (Git Credential Manager
        // under WSL) would wait on a window.
        text.Append("[credential]\n\thelper = \"\"\n");
        if (_credential is not null) text.Append($"\thelper = {Quote(_credential)}\n");
        // Commits attribute by email, whoever's token pushes.
        text.Append($"[user]\n\tname = {Quote(_name)}\n\temail = {Quote(_email)}\n");

        var fresh = config + ".harness";
        File.WriteAllText(fresh, text.ToString());
        File.Move(fresh, config, overwrite: true);

        var hooks = Path.Combine(Bare, "hooks");
        if (Directory.Exists(hooks)) Directory.Delete(hooks, recursive: true);
        var worktrees = Path.Combine(Bare, "worktrees");
        var dirs = Directory.Exists(worktrees) ? Directory.GetDirectories(worktrees).Prepend(Bare) : [Bare];
        foreach (var d in dirs) File.Delete(Path.Combine(d, "config.worktree"));
    }

    async Task<string> PrepareAt(string path)
    {
        await _git.WaitAsync();
        try
        {
            if (!Directory.Exists(Bare))
            {
                // The clone carries its own helpers: the repository's config does not exist yet, and a
                // machine-wide helper (Git Credential Manager under WSL) would wait on a window.
                try
                {
                    await Shell.Git(_root, ["-c", "credential.helper=", .. Helper(), "clone", "--bare", _repoUrl, Bare]);
                }
                catch
                {
                    if (Directory.Exists(Bare)) Directory.Delete(Bare, recursive: true);
                    throw;
                }
            }
            await WriteConfig();
            await Shell.Git(Bare, [.. Guard, "fetch", "--prune", "-q", _repoUrl, "+refs/heads/*:refs/remotes/origin/*"]);
            // A local story branch left by an earlier job would shadow the remote one a
            // later round must continue; every branch not checked out in a slot goes, unless it holds commits no remote has.
            var heads = await Shell.Git(Bare, "for-each-ref", "--format=%(refname:short)", "refs/heads");
            foreach (var head in heads.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (head == _branch) continue;
                // A branch with commits no remote ref holds is unpushed work: it stays.
                var unpushed = await Shell.Run("git", ["rev-list", "--count", $"refs/heads/{head}", "--not", "--remotes"], Bare);
                if (unpushed.ExitCode == 0 && int.TryParse(unpushed.Output.Trim(), out var n) && n > 0) continue;
                await Shell.Run("git", ["branch", "-D", head], Bare);
            }

            if (!Directory.Exists(Path.Combine(path, ".git")) && !File.Exists(Path.Combine(path, ".git")))
            {
                await Shell.Git(Bare, "worktree", "prune");
                await Shell.Git(Bare, "worktree", "add", "-f", "--detach", path, Tip);
            }
        }
        finally
        {
            _git.Release();
        }

        var dir = path;
        await Shell.Run("git", ["merge", "--abort"], dir);
        await Shell.Run("git", ["rebase", "--abort"], dir);
        await Shell.Git(dir, "checkout", "-q", "--detach", "-f", Tip);
        await Shell.Git(dir, "reset", "--hard", "-q", Tip);
        await Shell.Git(dir, "clean", "-ffd", "-q");
        return dir;
    }
}
