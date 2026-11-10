// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Security;

/// <summary>The key that seals what the current work stores. Read at every seal and open, so a key
/// written while the process runs is honoured and a tenant is never sealed under another's.</summary>
public interface IEncryptionKeySource
{
    /// <summary>Null or blank when the current work has no key.</summary>
    string? Current { get; }
}
