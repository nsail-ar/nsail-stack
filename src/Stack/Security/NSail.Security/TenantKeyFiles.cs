// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Security;

/// <summary>One key per tenant, read from <c>{directory}/{slug}.key</c>. The file is the cell's
/// copy of a key kept elsewhere; a tenant with no file has no key, and no default stands in.</summary>
public sealed class TenantKeyFiles : IEncryptionKeySource
{
    readonly string _directory;
    readonly Func<string?> _tenant;

    public TenantKeyFiles(string directory, Func<string?> tenant)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentNullException.ThrowIfNull(tenant);

        _directory = directory;
        _tenant = tenant;
    }

    public string? Current
    {
        get
        {
            var slug = _tenant();

            if (string.IsNullOrWhiteSpace(slug))
            {
                return null;
            }

            return Read(slug);
        }
    }

    public string? Read(string slug)
    {
        // The slug names a file: anything that could leave the directory is no tenant's key.
        if (slug.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            return null;
        }

        var path = Path.Combine(_directory, slug + ".key");

        return File.Exists(path) ? File.ReadAllText(path).Trim() : null;
    }
}
