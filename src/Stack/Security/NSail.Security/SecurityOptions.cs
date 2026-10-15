// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Security;

/// <summary>The <c>Security:</c> section — what an install decides about the credentials it
/// mints, as opposed to what a policy decides about a caller.</summary>
public class SecurityOptions
{
    /// <summary>The generation of ticket this install honours. Sign-in stamps it into the
    /// credential and <c>TicketEpochMiddleware</c> compares it on every authenticated request,
    /// so raising the number signs out everyone holding an older one — and nobody else.
    ///
    /// <para>It exists because the DataProtection key ring is the SERVER's and not the
    /// install's: every install on a box unprotects with the same keys, so there is no key to
    /// delete or revoke that would void one install's tickets without making every sealed
    /// secret on the box — the storage credential, the ARCA certificate — unreadable for good.
    /// This is the cut that is only this install's, and it touches nothing that was
    /// sealed.</para>
    ///
    /// <para>Zero is "never raised": no claim is minted and nothing is refused, so an install
    /// that has never needed the cut carries neither the byte nor the risk. From one upward the
    /// comparison is equality — a ticket stamped with ANY other generation, the unstamped one
    /// included, names a session this install no longer honours.</para></summary>
    public int TicketEpoch { get; set; }
}
