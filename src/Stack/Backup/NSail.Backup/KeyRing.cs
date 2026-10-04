// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.Backup;

/// <summary>Where the DataProtection key ring lives for an install. Deliberate rather than
/// the framework default, which picks a machine-dependent home the archive cannot find and a
/// restored install would not inherit: without the key ring every sealed secret in a restored
/// install — the storage credential, the Google grant, the ARCA certificate — is unreadable.</summary>
public static class KeyRing
{
    public const string FolderName = "keyring";

    public const string ArchiveFolder = "keyring/";

    public static DirectoryInfo Home(string contentRootPath)
    {
        var path = Path.Combine(contentRootPath, "data", FolderName);

        return new DirectoryInfo(path);
    }
}
