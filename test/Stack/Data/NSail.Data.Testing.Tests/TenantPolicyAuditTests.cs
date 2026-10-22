// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Data.Testing;
using Xunit;

namespace NSail.Data.Testing.Tests;

public class TenantPolicyAuditTests
{
    // What a product reads when its sweep reds, which is the whole value of the sweep: a table
    // nobody walled and the one line a migration adds to wall it, ready to paste. Measured here
    // because the only other place it is exercised is a failing run nobody wants to depend on.
    [Fact]
    public void AFailingSweepNamesTheTablesAndTheCallThatWallsThem()
    {
        var said = TenantPolicyAudit.Describe("Optical", ["Contacts", "Prescriptions"]);

        Assert.Contains("Optical carries 2 tenant-scoped table(s)", said, StringComparison.Ordinal);
        Assert.Contains("migrationBuilder.AddTenantPolicies(\"Contacts\", \"Prescriptions\");", said, StringComparison.Ordinal);
    }
}
