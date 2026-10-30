// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Problems;
using NSail.Storage.Providers;
using System.Diagnostics;
using System.Text;

namespace NSail.Storage.Git;

// A repository as the store: an object is a file at its key, a put or a delete is a commit
// pushed to the branch, and a get or a list reads the branch's tree. The repository is
// `Endpoint/Container`, the way S3 is `Endpoint/Container` with a bucket, and the credential is
// sent as an HTTP basic header (AccessKey the user, SecretKey the token).
//
// It runs the git executable the host has and holds nothing between calls: each verb works in a
// throwaway one-commit clone, so no state outlives it and two hosts never share a checkout.
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
            using var clone = await Clone(target, cancelToken);
            var path = clone.Resolve(key);

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            await using (var file = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                await content.CopyToAsync(file, 81920, cancelToken);
            }

            if (!await clone.Commit($"Put {key}", key, cancelToken) || await clone.Push(cancelToken))
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
        using var clone = await Clone(target, cancelToken);
        var path = clone.Resolve(key);

        if (!File.Exists(path))
        {
            return null;
        }

        // Copied out of the clone, which goes with this call, into a file that goes with the read.
        var copy = new FileStream(Path.GetTempFileName(), FileMode.Create, FileAccess.ReadWrite, FileShare.None, 81920, FileOptions.DeleteOnClose | FileOptions.Asynchronous);

        await using (var source = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true))
        {
            await source.CopyToAsync(copy, 81920, cancelToken);
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
        using var clone = await Clone(target, cancelToken);

        if (!clone.HasCommit)
        {
            return [];
        }

        var modifiedAt = DateTimeOffset.Parse((await clone.Run(cancelToken, "log", "-1", "--format=%cI")).Trim());

        return clone.Files()
            .Where(entry => prefix is not { Length: > 0 } || entry.Key.StartsWith(prefix, StringComparison.Ordinal))
            .Select(entry => new StoredObject
            {
                Key = entry.Key,
                Length = entry.Length,
                ModifiedAt = modifiedAt,
            })
            .OrderBy(stored => stored.Key, StringComparer.Ordinal)
            .ToList();
    }

    public async Task Delete(StorageTarget target, string key, CancellationToken cancelToken = default)
    {
        for (var attempt = 1; ; attempt++)
        {
            using var clone = await Clone(target, cancelToken);
            var path = clone.Resolve(key);

            if (!File.Exists(path))
            {
                return;
            }

            File.Delete(path);

            if (!await clone.Commit($"Delete {key}", key, cancelToken) || await clone.Push(cancelToken))
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

    static async Task<Checkout> Clone(StorageTarget target, CancellationToken cancelToken)
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
            await clone.Open(cancelToken);

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

        public string Resolve(string key)
        {
            var path = Path.GetFullPath(Path.Combine(_root, key));

            // The second lock on the door StorageManager.Guard closed, and the repository's own
            // metadata is no object either.
            if (!path.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                || key.Split('/')[0] == ".git")
            {
                throw new BusinessException(BusinessProblem.RuleViolation(
                    "KeyOutsideContainer",
                    $"'{key}' resolves outside the configured container.",
                    new Dictionary<string, string> { ["key"] = key }));
            }

            return path;
        }

        public async Task Open(CancellationToken cancelToken)
        {
            Directory.CreateDirectory(_root);

            var address = $"{_target.Endpoint.TrimEnd('/')}/{_target.Container.Trim('/')}";

            await Run(cancelToken, "init", "--quiet");
            await Run(cancelToken, "remote", "add", "origin", address);

            var (code, _, error) = await Execute(cancelToken, "fetch", "--quiet", "--depth", "1", "origin", _branch);

            if (code == 0)
            {
                await Run(cancelToken, "checkout", "--quiet", "-B", _branch, "FETCH_HEAD");
                HasCommit = true;
            }
            else if (error.Contains("couldn't find remote ref", StringComparison.OrdinalIgnoreCase))
            {
                await Run(cancelToken, "checkout", "--quiet", "--orphan", _branch);
            }
            else
            {
                throw Refused(Describe(error));
            }
        }

        // False when the tree already holds what was asked: nothing to commit, nothing to push.
        public async Task<bool> Commit(string message, string key, CancellationToken cancelToken)
        {
            await Run(cancelToken, "add", "--all");

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

            if (error.Contains("rejected", StringComparison.OrdinalIgnoreCase) || error.Contains("fetch first", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            throw Refused(Describe(error));
        }

        public IEnumerable<(string Key, long Length)> Files()
        {
            return Run(CancellationToken.None, "ls-tree", "-r", "-l", "-z", "HEAD").GetAwaiter().GetResult()
                .Split('\0', StringSplitOptions.RemoveEmptyEntries)
                .Select(line =>
                {
                    var tab = line.IndexOf('\t');
                    var size = line[..tab].Split(' ', StringSplitOptions.RemoveEmptyEntries)[3];

                    return (line[(tab + 1)..], long.Parse(size));
                });
        }

        public async Task<string> Run(CancellationToken cancelToken, params string[] arguments)
        {
            var (code, output, error) = await Execute(cancelToken, arguments);

            if (code != 0)
            {
                throw Refused(Describe(error));
            }

            return output;
        }

        async Task<(int Code, string Output, string Error)> Execute(CancellationToken cancelToken, params string[] arguments)
        {
            var info = new ProcessStartInfo("git")
            {
                WorkingDirectory = _root,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
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

            // The credential rides the environment of this one process, never its command line.
            if (_target.SecretKey.Length > 0)
            {
                var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_target.AccessKey}:{_target.SecretKey}"));

                info.Environment["GIT_CONFIG_COUNT"] = "1";
                info.Environment["GIT_CONFIG_KEY_0"] = "http.extraHeader";
                info.Environment["GIT_CONFIG_VALUE_0"] = $"Authorization: Basic {basic}";
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
                process.StandardInput.Close();

                var output = process.StandardOutput.ReadToEndAsync(cancelToken);
                var error = process.StandardError.ReadToEndAsync(cancelToken);

                try
                {
                    await process.WaitForExitAsync(cancelToken);
                }
                catch (OperationCanceledException)
                {
                    process.Kill(entireProcessTree: true);

                    throw;
                }

                return (process.ExitCode, await output, await error);
            }
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
        // of the address is the address the host configured.
        static string Describe(string error)
        {
            var text = error.Trim();

            return text.Length > 300 ? text[..300] : text;
        }
    }
}
