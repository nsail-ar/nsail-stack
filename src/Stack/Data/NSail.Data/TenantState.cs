// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Data;

/// <summary>What the INSTALL records about a tenant: whether it is served at all, and which plan
/// it is on. One row per tenant, written from outside the app by whoever operates the fleet and
/// read by every product the same way — the state of a customer is not a product's question and
/// must not be answered twice.
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
/// writer's hand must not take a paying customer down (data-tenancy.md, Tenant state).</para></summary>
public class TenantState : IInstallScoped
{
    /// <summary>The only word that stops a tenant being served.</summary>
    public const string Suspended = "Suspended";

    /// <summary>What a tenant nobody has acted on would say if it had a row.</summary>
    public const string Active = "Active";

    public Guid TenantId { get; set; }

    /// <summary>The word the writer put there, compared and never parsed: an unknown one reads
    /// as active. Text rather than an enum because the column IS the contract with a program
    /// outside this codebase — a row a person can read, and a value no deserializer can throw
    /// on.</summary>
    public string Status { get; set; } = Active;

    /// <summary>Which plan the customer is on, carried and not interpreted: the Stack names no
    /// plan and knows none. Whoever sells them says what the words are, and a product that
    /// limits by plan reads this.</summary>
    public string? Plan { get; set; }

    public bool IsSuspended
    {
        get { return string.Equals(Status, Suspended, StringComparison.OrdinalIgnoreCase); }
    }
}
