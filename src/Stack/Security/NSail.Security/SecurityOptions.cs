// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Security;

/// <summary>The <c>Security:</c> section — what an install decides about the credentials it
/// mints, as opposed to what a policy decides about a caller.</summary>
public class SecurityOptions
{
    /// <summary>The generation of ticket this install honours. Sign-in stamps it into the
    /// credential and <c>TicketEpochMiddleware</c> compares it on every authenticated request,
    /// so raising the number signs out everyone holding an older one — and nobody else. An open
    /// circuit meets it on its next gated send, as the same 401.
    ///
    /// <para>It exists because no DataProtection key can be deleted or revoked to void
    /// tickets alone: the key ring is the install's, and deleting a key makes every secret it
    /// sealed — the storage credential, the ARCA certificate — unreadable for good.
    /// This is the cut that is only this install's, and it touches nothing that was
    /// sealed.</para>
    ///
    /// <para>Zero is "never raised": no claim is minted and nothing is refused, so an install
    /// that has never needed the cut carries neither the byte nor the risk. From one upward the
    /// comparison is equality — a ticket stamped with ANY other generation, the unstamped one
    /// included, names a session this install no longer honours.</para></summary>
    public int TicketEpoch { get; set; }

    /// <summary>The proxies whose <c>X-Forwarded-*</c> headers are believed when
    /// <c>ASPNETCORE_FORWARDEDHEADERS_ENABLED</c> is on: addresses or CIDR networks
    /// (<c>10.0.0.0/8</c>). A header from any other peer is ignored, so a caller who reaches the
    /// app port directly cannot pick its own address. Empty trusts nobody.</summary>
    public string[] TrustedProxies { get; set; } = [];

    /// <summary>The directory holding one <c>{slug}.key</c> file per tenant, which
    /// <c>AddTenantKeys</c> reads to seal that tenant's stored secrets. Outside Development it is
    /// required.</summary>
    public string? KeysDirectory { get; set; }
}
