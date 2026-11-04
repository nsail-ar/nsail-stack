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
                await Shell.Git(Bare, "config", "remote.origin.fetch", "+refs/heads/*:refs/remotes/origin/*");
                // The empty helper first resets the list: a machine-wide helper (Git Credential Manager
                // under WSL) would wait on a window.
                await Shell.Git(Bare, "config", "--add", "credential.helper", "");
                if (_credential is not null) await Shell.Git(Bare, "config", "--add", "credential.helper", _credential);
            }
            // Commits attribute by email, whoever's token pushes.
            await Shell.Git(Bare, "config", "user.name", _name);
            await Shell.Git(Bare, "config", "user.email", _email);
            // A slot checks files out as the repository holds them (LF), as on Linux: a CRLF checkout
            // makes a test reading a file or a template see bytes CI never sees.
            await Shell.Git(Bare, "config", "core.autocrlf", "false");
            await Shell.Git(Bare, "config", "core.eol", "lf");
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
