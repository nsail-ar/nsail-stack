// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Data;

/// <summary>What the INSTALL records about a tenant: whether it is served at all, why, until
/// when, which plan it is on and what that plan includes. One row per tenant, written from
/// outside the app by whoever operates the fleet and read by every product the same way — the
/// state of a customer is not a product's question and must not be answered twice.
///
/// <para>It is <see cref="IInstallScoped"/>, and that is the whole of why the tenant column is
/// not on it: this is the install's record ABOUT a tenant, not a tenant's own row, and nothing
/// inside the app writes it. So <see cref="TenantId"/> is a column of its own — the tenant's
/// <see cref="Tenant.RowKey"/>, exactly what the shadow column carries everywhere else, which is
/// <see cref="Guid.Empty"/> wherever the wall is the connection or there are no tenants. One
/// shape in every mode, and one query can read the whole fleet's state where the fleet shares a
/// database.</para>
///
/// <para><b>A tenant with no row is active.</b> Absence is the normal state of every tenant
/// nobody has had to act on, so the table is empty until something is suspended, and a word in
/// <see cref="Status"/> that this Stack does not know is not a suspension either — a typo in the
/// writer's hand must not take a paying customer down (data-tenancy.md, Tenant state).</para>
///
/// <para><b>A pause is either a switch or a window.</b> <see cref="Status"/> is the switch,
/// immediate and open-ended; <see cref="SuspendedFrom"/> and <see cref="SuspendedUntil"/> are the
/// window a maintenance stop, a move or a billing cut is written ahead of time. They are
/// independent, OR'd by <see cref="IsSuspendedAt"/>, and nothing in the app flips one into the
/// other: the window is judged against the clock on every request, so no job has to run at the
/// minute it opens or closes.</para></summary>
public class TenantState : IInstallScoped
{
    /// <summary>The word that stops a tenant being served outright, with no end stated.</summary>
    public const string Suspended = "Suspended";

    /// <summary>What a tenant nobody has acted on would say if it had a row.</summary>
    public const string Active = "Active";

    /// <summary>Why a tenant is paused, as the words <see cref="Reason"/> is written with. They
    /// are the whole vocabulary: a deploy or a big change is <see cref="Maintenance"/>, and a
    /// word this Stack does not know reads as no reason at all.</summary>
    public static class Reasons
    {
        /// <summary>Work on the service — a deploy, a big change, a maintenance stop.</summary>
        public const string Maintenance = "Maintenance";

        /// <summary>The install is moving, between rings or between servers.</summary>
        public const string Move = "Move";

        /// <summary>The customer is not paying.</summary>
        public const string Payment = "Payment";
    }

    public Guid TenantId { get; set; }

    /// <summary>The word the writer put there, compared and never parsed: an unknown one reads
    /// as active. Text rather than an enum because the column IS the contract with a program
    /// outside this codebase — a row a person can read, and a value no deserializer can throw
    /// on.</summary>
    [NotPersonal]
    public string Status { get; set; } = Active;

    /// <summary>Which plan the customer is on, carried and not interpreted: the Stack names no
    /// plan and knows none. Whoever sells them says what the words are, and a product that
    /// limits by plan reads this.</summary>
    [NotPersonal]
    public string? Plan { get; set; }

    /// <summary>What that plan includes, as the bag was written: a JSON object of one key against
    /// a flag or a number, carried and not interpreted — <b>the Stack names no key and knows
    /// none</b>, exactly as it names no plan above. Text rather than a mapped shape for the same
    /// reason the rest of this row is text: the column IS the contract with a program outside
    /// this codebase, which writes it in plain SQL, and a bag no parser can read must leave a
    /// paying customer where it found them. Whoever reads it reads it through
    /// <c>Entitlements.Read</c> (NSail.Security), which answers the permissive thing to
    /// everything it cannot read.</summary>
    [NotPersonal]
    public string? Entitlements { get; set; }

    /// <summary>When the bag above was last written, in UTC. Its writer is a program outside
    /// this process, so "has my write landed" is a real question an operator asks of the row —
    /// and the memo in front of the read is why the answer is not immediate.</summary>
    public DateTime? EntitlementsUpdatedAt { get; set; }

    /// <summary>Why the tenant is paused, a word of <see cref="Reasons"/> — compared and never
    /// parsed, like <see cref="Status"/>. It changes nothing about whether the tenant is served:
    /// it is what the suspended page words itself by, and an unknown word gets the generic
    /// words.</summary>
    [NotPersonal]
    public string? Reason { get; set; }

    /// <summary>The moment the window opens, in UTC. A row with no <c>SuspendedFrom</c> has no
    /// window: an end with no beginning would be a suspension nobody asked for, so the pause
    /// that has only an end is the <see cref="Suspended"/> switch wearing an
    /// <see cref="SuspendedUntil"/> the page can say.</summary>
    public DateTime? SuspendedFrom { get; set; }

    /// <summary>The moment the window closes, in UTC, or null for a window with no end. It also
    /// says when the service comes back, which the page states whenever it is still ahead —
    /// including beside the <see cref="Suspended"/> switch, which it does not lift.</summary>
    public DateTime? SuspendedUntil { get; set; }

    /// <summary>What the writer wants this tenant's people to read, instead of nothing — plain
    /// text, shown under the generic words. Trusted and escaped anyway: the page is markup
    /// (data-tenancy.md, Tenant state).</summary>
    [NotPersonal]
    public string? Message { get; set; }

    /// <summary>Whether the tenant is paused at <paramref name="now"/>: the open-ended switch,
    /// or a window that has opened and not yet closed. A row with neither is active, which is
    /// also what a row carrying only a plan or only a message is.</summary>
    public bool IsSuspendedAt(DateTime now)
    {
        if (string.Equals(Status, Suspended, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return SuspendedFrom is { } from
            && now >= from
            && (SuspendedUntil is not { } until || now < until);
    }
}
