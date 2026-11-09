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

    /// <summary>The fingerprint of the memberships and roles this party holds now, for the
    /// install to compare with the one the ticket was minted with (<see cref="Session.MembershipStamp"/>).
    /// Null means this host keeps no such record, and nothing is compared. Any change to the
    /// party's memberships or their roles must change the value; an unchanged party must
    /// answer the same one every time.</summary>
    public virtual Task<string?> MembershipStamp(Guid partyId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<string?>(null);
    }
}
