// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Security;

/// <summary>Answers whether the credential a request arrived with still names somebody this
/// install has — and brings it up to date where only its contents went stale.
/// <para>A signed ticket outlives the rows it names: a party merged away, a user disabled or
/// deleted, a user repointed at another party. Until it is asked, the session keeps naming the
/// dead id and writes it into every row it touches, which the database refuses as a foreign
/// key and the person reads as a 500.</para>
/// <para>The base answers yes, the RelationProvider shape: a host that keeps no records — the
/// WASM client, a product without Iam — has nothing to check a ticket against. Iam registers
/// the DB-backed one.</para></summary>
public class CredentialProvider
{
    /// <summary>False means the credential names nobody this install still has, and the caller
    /// must be signed out. True covers both the credential that was already true and the one
    /// this call re-issued.</summary>
    public virtual Task<bool> Holds(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }
}
