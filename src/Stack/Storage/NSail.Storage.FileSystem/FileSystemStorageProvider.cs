// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Icons;
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

    public string Type
    {
        get { return ProviderType; }
    }

    public string DisplayName
    {
        get { return "Local folder"; }
    }

    // Material Symbols folder, carried here rather than named from NsIcons: a kit's server
    // assembly does not reference the component library.
    public Glyph Icon
    {
        get
        {
            return new("""<g transform="translate(0,24) scale(0.025)"><path d="M144-192q-29.7 0-50.85-21.15Q72-234.3 72-264v-432q0-29.7 21.15-50.85Q114.3-768 144-768h242l72 72h358q29.7 0 50.85 21.15Q888-653.7 888-624v360q0 29.7-21.15 50.85Q845.7-192 816-192H144Zm0-72h672v-360H428l-72-72H144v432Zm0 0v-432 432Z"/></g>""");
        }
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

        await using var file = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize, useAsync: true);

        await content.CopyToAsync(file, BufferSize, cancelToken);
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

        if (!Directory.Exists(root))
        {
            return Task.FromResult<IReadOnlyList<StoredObject>>([]);
        }

        var objects = Directory
            .EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(path => new { Path = path, Key = Path.GetRelativePath(root, path).Replace('\\', '/') })
            .Where(entry => prefix is not { Length: > 0 } || entry.Key.StartsWith(prefix, StringComparison.Ordinal))
            .Select(entry => new StoredObject
            {
                Key = entry.Key,
                Length = new FileInfo(entry.Path).Length,
                ModifiedAt = File.GetLastWriteTimeUtc(entry.Path),
            })
            .OrderBy(stored => stored.Key, StringComparer.Ordinal)
            .ToList();

        return Task.FromResult<IReadOnlyList<StoredObject>>(objects);
    }

    public Task Delete(StorageTarget target, string key, CancellationToken cancelToken = default)
    {
        File.Delete(Resolve(target, key));

        return Task.CompletedTask;
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
