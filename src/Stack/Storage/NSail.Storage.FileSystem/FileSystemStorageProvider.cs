// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.IO.Enumeration;
using NSail.Problems;
using NSail.Storage.Providers;

namespace NSail.Storage.FileSystem;

// The provider with no account anywhere: the container is a directory on the machine the host
// runs on, and an object is a file under it. It is what makes a download-to-disk possible on an
// install that has bought nothing.
public class FileSystemStorageProvider : IStorageProvider
{
    public const string ProviderType = "file";

    // A stream copied straight through, so the size of the object never becomes the size of a
    // buffer. The number is the BCL's own default for CopyTo, restated here because the
    // constructor overload that takes useAsync has no default to fall back on.
    const int BufferSize = 81920;

    // Marks a Put still being copied; List never reports one. Only this exact form is skipped
    // (prefix plus the 32 hex digits of Guid "N"), so a key that merely starts with the prefix
    // stays listable.
    const string StagingPrefix = ".put-";

    static readonly int StagingNameLength = StagingPrefix.Length + 32;

    public string Type
    {
        get { return ProviderType; }
    }

    public string DisplayName
    {
        get { return "Local folder"; }
    }

    public bool IsRemote
    {
        get { return false; }
    }

    public async Task Put(StorageTarget target, string key, Stream content, string? contentType, CancellationToken cancelToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        var path = Resolve(target, key);

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        // Copied beside the target and renamed over it: a cancel or a throwing source leaves the
        // previous object untouched and never a partial file under the key.
        var staging = Path.Combine(Path.GetDirectoryName(path)!, StagingPrefix + Guid.NewGuid().ToString("N"));

        try
        {
            await using (var file = new FileStream(staging, FileMode.CreateNew, FileAccess.Write, FileShare.None, BufferSize, useAsync: true))
            {
                await content.CopyToAsync(file, BufferSize, cancelToken);
            }

            File.Move(staging, path, overwrite: true);
        }
        catch
        {
            try
            {
                File.Delete(staging);
            }
            catch (Exception)
            {
                // The original failure is the one the caller must see.
            }

            throw;
        }
    }

    public Task<ObjectContent?> Get(StorageTarget target, string key, CancellationToken cancelToken = default)
    {
        var path = Resolve(target, key);

        if (!File.Exists(path))
        {
            return Task.FromResult<ObjectContent?>(null);
        }

        // FileShare.Delete alongside Read, not Read alone: a caller that reads an object and then
        // deletes it must not have to know which operating system it is on — Windows forbids the
        // delete otherwise, POSIX never did.
        var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, BufferSize, useAsync: true);

        return Task.FromResult<ObjectContent?>(new ObjectContent
        {
            Content = file,
            Length = file.Length,
        });
    }

    public Task<IReadOnlyList<StoredObject>> List(StorageTarget target, string? prefix, CancellationToken cancelToken = default)
    {
        var root = Root(target);

        // Only the directory the prefix names is walked; the text after its last slash is a
        // filter on names inside it.
        var slash = prefix?.LastIndexOf('/') ?? -1;
        var directory = slash > 0 ? Path.GetFullPath(Path.Combine(root, prefix![..slash])) : root;
        var inside = directory == root
            || directory.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.Ordinal);

        if (!inside || !Directory.Exists(directory))
        {
            return Task.FromResult<IReadOnlyList<StoredObject>>([]);
        }

        // The constructor's defaults skip hidden files and swallow unreadable directories; these two
        // keep a dot-leaf listed and a short listing loud.
        var options = new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = 0, IgnoreInaccessible = false };
        var entries = new FileSystemEnumerable<StoredObject>(
            directory,
            (ref FileSystemEntry entry) => new StoredObject
            {
                Key = Path.GetRelativePath(root, entry.ToFullPath()).Replace('\\', '/'),
                Length = entry.Length,
                ModifiedAt = entry.LastWriteTimeUtc,
            },
            options)
        {
            ShouldIncludePredicate = (ref FileSystemEntry entry) => !entry.IsDirectory && !IsStaging(entry.FileName.ToString()),
        };

        var objects = entries
            .Where(stored => prefix is not { Length: > 0 } || stored.Key.StartsWith(prefix, StringComparison.Ordinal))
            .OrderBy(stored => stored.Key, StringComparer.Ordinal)
            .ToList();

        return Task.FromResult<IReadOnlyList<StoredObject>>(objects);
    }

    public Task Delete(StorageTarget target, string key, CancellationToken cancelToken = default)
    {
        var path = Resolve(target, key);

        // File.Delete throws when the directory is absent; a missing object is not a failure.
        if (Directory.Exists(Path.GetDirectoryName(path)))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    // A directory on the host has no address anyone outside the host could read it at.
    public Uri? Link(StorageTarget target, string key)
    {
        return null;
    }

    static bool IsStaging(string name)
    {
        return name.Length == StagingNameLength
            && name.StartsWith(StagingPrefix, StringComparison.Ordinal)
            && name.AsSpan(StagingPrefix.Length).IndexOfAnyExcept("0123456789abcdef") < 0;
    }

    static string Root(StorageTarget target)
    {
        return Path.GetFullPath(target.Container);
    }

    // The second lock on the same door StorageManager.Guard already closed: a key that reaches
    // this provider by any other route still cannot resolve outside its own container, because
    // what is compared is the RESOLVED path and not the text of the key.
    static string Resolve(StorageTarget target, string key)
    {
        var root = Root(target);
        var path = Path.GetFullPath(Path.Combine(root, key));

        if (!path.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new BusinessException(BusinessProblem.RuleViolation(
                "KeyOutsideContainer",
                $"'{key}' resolves outside the configured container.",
                new Dictionary<string, string> { ["key"] = key }));
        }

        return path;
    }
}
