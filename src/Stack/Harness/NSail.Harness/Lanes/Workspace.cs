// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

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

    // Caller holds _git. The file is rebuilt, not edited: only the keys git itself set on a
    // clone of this machine's platform are carried over.
    async Task WriteConfig()
    {
        var config = Path.Combine(Bare, "config");
        var kept = new List<(string Key, string Value)>();
        foreach (var key in new[] { "core.filemode", "core.symlinks", "core.ignorecase", "core.precomposeunicode" })
        {
            var r = await Shell.Run("git", ["config", "--file", config, "--get", key], _root);
            if (r.ExitCode == 0) kept.Add((key, r.Output.Trim()));
        }

        var fresh = config + ".harness";
        File.WriteAllText(fresh, "");
        await Shell.Git(_root, "config", "--file", fresh, "core.repositoryformatversion", "0");
        await Shell.Git(_root, "config", "--file", fresh, "core.bare", "true");
        foreach (var (key, value) in kept) await Shell.Git(_root, "config", "--file", fresh, key, value);
        await Shell.Git(_root, "config", "--file", fresh, "remote.origin.url", _repoUrl);
        await Shell.Git(_root, "config", "--file", fresh, "remote.origin.fetch", "+refs/heads/*:refs/remotes/origin/*");
        // The empty helper first resets the list: a machine-wide helper (Git Credential Manager
        // under WSL) would wait on a window.
        await Shell.Git(_root, "config", "--file", fresh, "--add", "credential.helper", "");
        if (_credential is not null) await Shell.Git(_root, "config", "--file", fresh, "--add", "credential.helper", _credential);
        // Commits attribute by email, whoever's token pushes.
        await Shell.Git(_root, "config", "--file", fresh, "user.name", _name);
        await Shell.Git(_root, "config", "--file", fresh, "user.email", _email);
        // A slot checks files out as the repository holds them (LF), as on Linux: a CRLF checkout
        // makes a test reading a file or a template see bytes CI never sees.
        await Shell.Git(_root, "config", "--file", fresh, "core.autocrlf", "false");
        await Shell.Git(_root, "config", "--file", fresh, "core.eol", "lf");
        File.Move(fresh, config, overwrite: true);

        var hooks = Path.Combine(Bare, "hooks");
        if (Directory.Exists(hooks)) Directory.Delete(hooks, recursive: true);
        foreach (var f in Directory.EnumerateFiles(Bare, "config.worktree", SearchOption.AllDirectories)) File.Delete(f);
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
            await Shell.Git(Bare, "fetch", "--prune", "-q", "origin");
            // A local story branch left by an earlier job would shadow the remote one a
            // later round must continue; every branch not checked out in a slot goes.
            var heads = await Shell.Git(Bare, "for-each-ref", "--format=%(refname:short)", "refs/heads");
            foreach (var head in heads.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (head != _branch) await Shell.Run("git", ["branch", "-D", head], Bare);
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
