// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Problems;
using NSail.Storage.Providers;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace NSail.Storage.Git;

// A repository as the store: an object is a file at its key, a put or a delete is a commit
// pushed to the branch, and a get or a list reads the branch's tree. The repository is
// `Endpoint/Container`, the way S3 is `Endpoint/Container` with a bucket, and the credential is
// sent as an HTTP basic header (AccessKey the user, SecretKey the token) to the repository's
// origin only.
//
// It runs the git executable the host has and holds nothing between calls: each verb works in a
// throwaway one-commit clone, so no state outlives it and two hosts never share a checkout. A
// write checks the branch out to commit in it; a read fetches the tree without its files and then
// only the files it answers with, so a small object costs no more in a large repository.
public class GitStorageProvider : IStorageProvider
{
    public const string ProviderType = "git";

    const string DefaultBranch = "main";

    // A push another writer won the race for is fetched and committed again this many times.
    const int PushAttempts = 4;

    public string Type
    {
        get { return ProviderType; }
    }

    public string DisplayName
    {
        get { return "Git repository"; }
    }

    public bool IsRemote
    {
        get { return true; }
    }

    public bool IsRepository
    {
        get { return true; }
    }

    public async Task Put(StorageTarget target, string key, Stream content, string? contentType, CancellationToken cancelToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        for (var attempt = 1; ; attempt++)
        {
            using var clone = await Clone(target, Reach.WorkingTree, cancelToken);
            var path = clone.Resolve(key);

            await clone.RefuseLinks(key, cancelToken);

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            await using (var file = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                await content.CopyToAsync(file, 81920, cancelToken);
            }

            if (!await clone.Commit($"Put {key}", cancelToken) || await clone.Push(cancelToken))
            {
                return;
            }

            if (attempt == PushAttempts || !content.CanSeek)
            {
                throw Refused("the push was rejected: the branch moved while the object was written");
            }

            content.Position = 0;
        }
    }

    public async Task<ObjectContent?> Get(StorageTarget target, string key, CancellationToken cancelToken = default)
    {
        using var clone = await Clone(target, Reach.Tree, cancelToken);

        // A key outside the container or into `.git` is refused before git reads it.
        clone.Resolve(key);

        await clone.RefuseLinks(key, cancelToken);

        var blob = await clone.Blob(key, cancelToken);

        if (blob is null)
        {
            return null;
        }

        await clone.Fetch([blob], cancelToken);

        // Copied out of the clone, which goes with this call, into a file that goes with the read.
        var copy = new FileStream(Path.GetTempFileName(), FileMode.Create, FileAccess.ReadWrite, FileShare.None, 81920, FileOptions.DeleteOnClose | FileOptions.Asynchronous);

        try
        {
            await clone.Read(blob, copy, cancelToken);
        }
        catch
        {
            await copy.DisposeAsync();

            throw;
        }

        copy.Position = 0;

        return new ObjectContent
        {
            Content = copy,
            Length = copy.Length,
        };
    }

    public async Task<IReadOnlyList<StoredObject>> List(StorageTarget target, string? prefix, CancellationToken cancelToken = default)
    {
        // A length needs its file: with a prefix, only the files under it are fetched.
        using var clone = await Clone(target, prefix is { Length: > 0 } ? Reach.Tree : Reach.Files, cancelToken);

        if (!clone.HasCommit)
        {
            return [];
        }

        // Every object carries the date of the branch's last commit: a per-file date would cost a
        // `git log` per key.
        var modifiedAt = DateTimeOffset.Parse((await clone.Run(cancelToken, "log", "-1", "--format=%cI")).Trim(), CultureInfo.InvariantCulture);

        var entries = (await clone.Blobs(cancelToken))
            .Where(entry => prefix is not { Length: > 0 } || entry.Key.StartsWith(prefix, StringComparison.Ordinal))
            .ToList();

        var lengths = await clone.Lengths(entries.Select(entry => entry.Blob).ToList(), cancelToken);

        return entries
            .Select(entry => new StoredObject
            {
                Key = entry.Key,
                Length = lengths[entry.Blob],
                ModifiedAt = modifiedAt,
            })
            .OrderBy(stored => stored.Key, StringComparer.Ordinal)
            .ToList();
    }

    public async Task Delete(StorageTarget target, string key, CancellationToken cancelToken = default)
    {
        for (var attempt = 1; ; attempt++)
        {
            using var clone = await Clone(target, Reach.WorkingTree, cancelToken);
            var path = clone.Resolve(key);

            await clone.RefuseLinks(key, cancelToken);

            if (!File.Exists(path))
            {
                return;
            }

            File.Delete(path);

            if (!await clone.Commit($"Delete {key}", cancelToken) || await clone.Push(cancelToken))
            {
                return;
            }

            if (attempt == PushAttempts)
            {
                throw Refused("the push was rejected: the branch moved while the object was removed");
            }
        }
    }

    // Each segment escaped on its own, so the key's slashes stay its folders.
    public Uri? Link(StorageTarget target, string key)
    {
        if (!target.LinkTemplate.Contains("{key}", StringComparison.Ordinal))
        {
            return null;
        }

        var escaped = string.Join('/', key.Split('/').Select(Uri.EscapeDataString));

        return Uri.TryCreate(target.LinkTemplate.Replace("{key}", escaped, StringComparison.Ordinal), UriKind.Absolute, out var link)
            ? link
            : null;
    }

    static async Task<Checkout> Clone(StorageTarget target, Reach reach, CancellationToken cancelToken)
    {
        if (target.Endpoint is not { Length: > 0 })
        {
            throw new BusinessException(BusinessProblem.RuleViolation(
                "StorageGitEndpointRequired",
                nameof(StorageTarget.Endpoint),
                "A Git target needs the address its repositories live under."));
        }

        var clone = new Checkout(target);

        try
        {
            await clone.Open(reach, cancelToken);

            return clone;
        }
        catch
        {
            clone.Dispose();

            throw;
        }
    }

    static BusinessException Refused(string detail)
    {
        return new BusinessException(BusinessProblem.RuleViolation(
            "GitRefused",
            $"git answered: {detail}",
            new Dictionary<string, string> { ["detail"] = detail }));
    }

    static BusinessException Invalid(string key, string property, string message)
    {
        return new BusinessException(BusinessProblem.RuleViolation(key, property, message));
    }

    // What a verb needs of the branch's last commit: its tree alone, its tree with every file, or
    // a working tree to commit in.
    enum Reach
    {
        Tree,
        Files,
        WorkingTree,
    }

    // One working directory holding the branch's last commit, deleted with it.
    sealed class Checkout : IDisposable
    {
        readonly StorageTarget _target;
        readonly string _root = Path.Combine(Path.GetTempPath(), "nsail-git-" + Guid.NewGuid().ToString("N"));
        readonly string _branch;

        public Checkout(StorageTarget target)
        {
            _target = target;
            _branch = target.Branch.Length > 0 ? target.Branch : DefaultBranch;
        }

        public bool HasCommit { get; private set; }

        string Address
        {
            get { return $"{_target.Endpoint.TrimEnd('/')}/{_target.Container.Trim('/')}"; }
        }

        public string Resolve(string key)
        {
            var path = Path.GetFullPath(Path.Combine(_root, key));

            // The second lock on the door StorageManager.Guard closed. Git's own metadata is no
            // object either, at any depth and in any case: git skips a `.git` component wherever
            // it is.
            if (!path.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                || key.Split('/', '\\').Any(segment => segment.Equals(".git", StringComparison.OrdinalIgnoreCase)))
            {
                throw new BusinessException(BusinessProblem.RuleViolation(
                    "KeyOutsideContainer",
                    $"'{key}' resolves outside the configured container.",
                    new Dictionary<string, string> { ["key"] = key }));
            }

            return path;
        }

        // A symlink the repository holds, at the key or on its way, would send a write outside the
        // clone: the key is refused. Read from the committed tree, so it holds on any platform.
        public async Task RefuseLinks(string key, CancellationToken cancelToken)
        {
            if (!HasCommit)
            {
                return;
            }

            var segments = key.Split('/', '\\', StringSplitOptions.RemoveEmptyEntries);
            var paths = new List<string>();

            for (var length = 1; length <= segments.Length; length++)
            {
                paths.Add(string.Join('/', segments.Take(length)));
            }

            var arguments = new List<string> { "--literal-pathspecs", "ls-tree", "-z", "HEAD", "--" };

            arguments.AddRange(paths);

            var listed = await Run(cancelToken, [.. arguments]);

            if (listed.Split('\0', StringSplitOptions.RemoveEmptyEntries).Any(line => line.StartsWith("120000 ", StringComparison.Ordinal)
                && paths.Contains(line[(line.IndexOf('\t') + 1)..], StringComparer.Ordinal)))
            {
                throw new BusinessException(BusinessProblem.RuleViolation(
                    "KeyThroughSymlink",
                    $"'{key}' passes through a symbolic link of the repository.",
                    new Dictionary<string, string> { ["key"] = key }));
            }
        }

        public async Task Open(Reach reach, CancellationToken cancelToken)
        {
            Directory.CreateDirectory(_root);

            var address = Address;

            // The address and the branch are typed on a settings screen: neither may read as an
            // option to git.
            if (address.StartsWith('-'))
            {
                throw Invalid("StorageGitAddressInvalid", nameof(StorageTarget.Endpoint), "The address of the repository is not valid.");
            }

            if (_branch.StartsWith('-'))
            {
                throw Invalid("StorageGitBranchInvalid", nameof(StorageTarget.Branch), "The branch name is not valid.");
            }

            var reference = $"refs/heads/{_branch}";

            await Run(cancelToken, "init", "--quiet");

            // check-ref-format says nothing on stderr when it refuses.
            if ((await Execute(cancelToken, "check-ref-format", reference)).Code != 0)
            {
                throw Invalid("StorageGitBranchInvalid", nameof(StorageTarget.Branch), "The branch name is not valid.");
            }

            // The repository's own attributes must not reach the bytes: no line-ending
            // conversion, no filter.
            Directory.CreateDirectory(Path.Combine(_root, ".git", "info"));
            await File.WriteAllTextAsync(Path.Combine(_root, ".git", "info", "attributes"), "* -text -eol -ident -filter -working-tree-encoding\n", cancelToken);

            await Run(cancelToken, "remote", "add", "origin", address);

            // Exit code 2 is "no such ref": a new branch, or an empty repository.
            var (code, _, error) = await Execute(cancelToken, "ls-remote", "--exit-code", "--heads", "origin", reference);

            if (code == 0 && reach == Reach.WorkingTree)
            {
                await Run(cancelToken, "fetch", "--quiet", "--depth", "1", "origin", reference);
                await Run(cancelToken, "checkout", "--quiet", "-B", _branch, "FETCH_HEAD");
                HasCommit = true;
            }
            else if (code == 0)
            {
                // A server that does not filter sends every file instead: slower, still right.
                await Run(cancelToken, reach == Reach.Tree
                    ? ["fetch", "--quiet", "--depth", "1", "--filter=blob:none", "origin", reference]
                    : ["fetch", "--quiet", "--depth", "1", "origin", reference]);
                await Run(cancelToken, "update-ref", "HEAD", "FETCH_HEAD");
                HasCommit = true;
            }
            else if (code == 2)
            {
                await Run(cancelToken, "checkout", "--quiet", "--orphan", _branch);
            }
            else
            {
                throw Refused(Describe(error));
            }
        }

        // False when the tree already holds what was asked: nothing to commit, nothing to push.
        // `--force` because the repository's own .gitignore must not drop an object unannounced.
        public async Task<bool> Commit(string message, CancellationToken cancelToken)
        {
            await Run(cancelToken, "add", "--all", "--force");

            if ((await Run(cancelToken, "status", "--porcelain")).Length == 0)
            {
                return false;
            }

            await Run(cancelToken, "-c", "user.name=NSail Storage", "-c", "user.email=storage@nsail.invalid", "commit", "--quiet", "--no-gpg-sign", "-m", message);

            return true;
        }

        // False when the remote refused because the branch moved, so the caller starts again from
        // its new head; anything else it refused is thrown.
        public async Task<bool> Push(CancellationToken cancelToken)
        {
            var (code, _, error) = await Execute(cancelToken, "push", "--quiet", "origin", $"HEAD:refs/heads/{_branch}");

            if (code == 0)
            {
                return true;
            }

            // The two reasons git gives for a branch that moved; a hook's refusal is not one.
            if (error.Contains("(fetch first)", StringComparison.Ordinal) || error.Contains("(non-fast-forward)", StringComparison.Ordinal))
            {
                return false;
            }

            throw Refused(Describe(error));
        }

        // Every file of the tree and the id of its content, read from the tree alone: asking for
        // sizes here would make git fetch every file.
        public async Task<IEnumerable<(string Key, string Blob)>> Blobs(CancellationToken cancelToken)
        {
            return Entries(await Run(cancelToken, "ls-tree", "-r", "-z", "HEAD"));
        }

        // The id of the file at the key, or null: a key that names a directory or a submodule is
        // no object.
        public async Task<string?> Blob(string key, CancellationToken cancelToken)
        {
            if (!HasCommit)
            {
                return null;
            }

            var listed = await Run(cancelToken, "--literal-pathspecs", "ls-tree", "-z", "HEAD", "--", key);

            return Entries(listed)
                .Where(entry => entry.Key == key)
                .Select(entry => entry.Blob)
                .FirstOrDefault();
        }

        // The files a filtered fetch left out, fetched by id in one request, the way git fetches
        // what a partial clone is missing.
        public async Task Fetch(IReadOnlyCollection<string> blobs, CancellationToken cancelToken)
        {
            var present = (await Run(cancelToken, "cat-file", "--batch-all-objects", "--batch-check=%(objectname)"))
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .ToHashSet(StringComparer.Ordinal);

            var missing = blobs.Where(blob => !present.Contains(blob)).Distinct().ToList();

            if (missing.Count == 0)
            {
                return;
            }

            await Feed(cancelToken, string.Join('\n', missing) + "\n",
                "-c", "fetch.negotiationAlgorithm=noop", "fetch", "--quiet", "--no-tags", "--no-write-fetch-head",
                "--recurse-submodules=no", "--filter=blob:none", "--stdin", "origin");
        }

        public async Task<Dictionary<string, long>> Lengths(IReadOnlyCollection<string> blobs, CancellationToken cancelToken)
        {
            if (blobs.Count == 0)
            {
                return [];
            }

            await Fetch(blobs, cancelToken);

            var checks = await Feed(cancelToken, string.Join('\n', blobs.Distinct()) + "\n", "cat-file", "--batch-check=%(objectname) %(objectsize)");
            var lengths = new Dictionary<string, long>(StringComparer.Ordinal);

            foreach (var line in checks.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var columns = line.Split(' ');

                // A file the fetch did not bring answers `<id> missing`.
                if (!long.TryParse(columns[1], NumberStyles.None, CultureInfo.InvariantCulture, out var length))
                {
                    throw Refused($"the file {columns[0]} could not be fetched");
                }

                lengths[columns[0]] = length;
            }

            return lengths;
        }

        // The file's bytes as the commit holds them: `cat-file` applies no attribute, filter or
        // line-ending conversion.
        public async Task Read(string blob, Stream destination, CancellationToken cancelToken)
        {
            var (code, _, error) = await Execute(cancelToken, null, destination, "cat-file", "blob", blob);

            if (code != 0)
            {
                throw Refused(Describe(error));
            }
        }

        public Task<string> Run(CancellationToken cancelToken, params string[] arguments)
        {
            return Feed(cancelToken, null, arguments);
        }

        async Task<string> Feed(CancellationToken cancelToken, string? input, params string[] arguments)
        {
            var (code, output, error) = await Execute(cancelToken, input, null, arguments);

            if (code != 0)
            {
                throw Refused(Describe(error));
            }

            return output;
        }

        Task<(int Code, string Output, string Error)> Execute(CancellationToken cancelToken, params string[] arguments)
        {
            return Execute(cancelToken, null, null, arguments);
        }

        // `input` goes to git's standard input; with a `sink`, the output is copied there as bytes
        // instead of read as text.
        async Task<(int Code, string Output, string Error)> Execute(CancellationToken cancelToken, string? input, Stream? sink, params string[] arguments)
        {
            var info = new ProcessStartInfo("git")
            {
                WorkingDirectory = _root,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                StandardInputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };

            foreach (var argument in arguments)
            {
                info.ArgumentList.Add(argument);
            }

            // Never a prompt: a missing credential is a refusal, not a process waiting on a
            // terminal nobody has.
            info.Environment["GIT_TERMINAL_PROMPT"] = "0";
            info.Environment["GIT_CONFIG_NOSYSTEM"] = "1";

            // Git's messages are read by code in one place, so they are read in one language.
            info.Environment["LC_ALL"] = "C";
            info.Environment["LANGUAGE"] = "C";

            // The address is typed on a settings screen: only the transports that move bytes,
            // never a helper that runs a command (`ext::`, `fd::`).
            info.Environment["GIT_ALLOW_PROTOCOL"] = "file:http:https:ssh:git";

            var config = new List<(string Key, string Value)>
            {
                ("protocol.ext.allow", "never"),
                ("core.autocrlf", "false"),
                ("core.symlinks", "false"),
            };

            // The credential rides the environment of this one process, never its command line,
            // and goes to the repository's origin only. git resolves the header once, for the
            // address it started with, and would carry it on to wherever a redirect moved that
            // address: so a redirect is refused, not followed.
            if (_target.SecretKey.Length > 0 && Origin(Address) is { } origin)
            {
                var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_target.AccessKey}:{_target.SecretKey}"));

                config.Add(($"http.{origin}.extraHeader", $"Authorization: Basic {basic}"));
                config.Add(($"http.{origin}.followRedirects", "false"));
            }

            info.Environment["GIT_CONFIG_COUNT"] = config.Count.ToString();

            for (var index = 0; index < config.Count; index++)
            {
                info.Environment[$"GIT_CONFIG_KEY_{index}"] = config[index].Key;
                info.Environment[$"GIT_CONFIG_VALUE_{index}"] = config[index].Value;
            }

            Process process;

            try
            {
                process = Process.Start(info)!;
            }
            catch (System.ComponentModel.Win32Exception)
            {
                throw Refused("the git executable is not available on this host");
            }

            using (process)
            {
                var output = sink is null
                    ? process.StandardOutput.ReadToEndAsync(cancelToken)
                    : Copy(process.StandardOutput.BaseStream, sink, cancelToken);
                var error = process.StandardError.ReadToEndAsync(cancelToken);

                try
                {
                    if (input is not null)
                    {
                        await process.StandardInput.WriteAsync(input.AsMemory(), cancelToken);
                    }

                    process.StandardInput.Close();

                    await process.WaitForExitAsync(cancelToken);
                }
                catch (Exception)
                {
                    process.Kill(entireProcessTree: true);

                    await Observe(output, error);

                    throw;
                }

                await Task.WhenAll(output, error);

                return (process.ExitCode, await output, await error);
            }
        }

        static async Task<string> Copy(Stream source, Stream sink, CancellationToken cancelToken)
        {
            await source.CopyToAsync(sink, 81920, cancelToken);

            return "";
        }

        // Both reads end with the killed process, faulted or cancelled: awaited here, so neither
        // is left unobserved behind the cancellation, or the failed write, this call throws.
        static async Task Observe(params Task[] reads)
        {
            try
            {
                await Task.WhenAll(reads);
            }
            catch (Exception)
            {
            }
        }

        // `scheme://host[:port]/`, which git matches against the address and every path under it.
        static string? Origin(string address)
        {
            return Uri.TryCreate(address, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
                ? $"{uri.Scheme}://{uri.Authority}/"
                : null;
        }

        // `ls-tree` lines are `<mode> <type> <id>\t<path>`; a submodule's type is `commit`.
        static IEnumerable<(string Key, string Blob)> Entries(string listed)
        {
            return listed
                .Split('\0', StringSplitOptions.RemoveEmptyEntries)
                .Select(line =>
                {
                    var tab = line.IndexOf('\t');
                    var columns = line[..tab].Split(' ', StringSplitOptions.RemoveEmptyEntries);

                    return (Key: line[(tab + 1)..], Type: columns[1], Blob: columns[2]);
                })
                .Where(entry => entry.Type == "blob")
                .Select(entry => (entry.Key, entry.Blob));
        }

        public void Dispose()
        {
            if (!Directory.Exists(_root))
            {
                return;
            }

            // Git marks its object files read-only, which Windows will not delete.
            foreach (var file in new DirectoryInfo(_root).EnumerateFiles("*", SearchOption.AllDirectories))
            {
                file.Attributes = FileAttributes.Normal;
            }

            Directory.Delete(_root, recursive: true);
        }

        // The credential never reaches an error: git does not echo headers, and what it echoes
        // of an address is the one the host configured or the one a refused redirect named.
        static string Describe(string error)
        {
            var text = error.Trim();

            return text.Length > 300 ? text[..300] : text;
        }
    }
}
