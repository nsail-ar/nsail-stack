// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Backup.Tests;

public sealed class RestorePlanTests
{
    static readonly string[] KnownSchemas = ["20260101000000_InitialCreate", "20260102000000_Seed"];

    const string InstallConnectionString = "Host=db.nsail.ar;Port=5432;Database=optical_main;Username=nsail;Password=s3cret";

    [Fact]
    public void AnArchiveFromASchemaThisBuildDoesNotKnowIsRefused()
    {
        var failure = Assert.Throws<BackupFailure>(() => RestorePlan.For(
            Install(),
            Manifest() with { SchemaVersion = "20261231000000_ThingsToCome" },
            "16.10",
            KnownSchemas));

        Assert.Contains("20261231000000_ThingsToCome", failure.Message);
        Assert.Contains("newer schema", failure.Message);
    }

    [Fact]
    public void AnArchiveFromAnotherPostgresMajorIsRefused()
    {
        var failure = Assert.Throws<BackupFailure>(() => RestorePlan.For(
            Install(),
            Manifest() with { PostgresVersion = "18.1" },
            "16.10",
            KnownSchemas));

        Assert.Contains("18.1", failure.Message);
        Assert.Contains("16.10", failure.Message);
    }

    [Fact]
    public void APatchOfTheSamePostgresMajorIsRestoredInto()
    {
        var plan = RestorePlan.For(Install(), Manifest() with { PostgresVersion = "16.4" }, "16.10", KnownSchemas);

        Assert.Equal("16.10", plan.TargetPostgresVersion);
    }

    [Fact]
    public void AnArchiveFromAnotherProductIsRefused()
    {
        var failure = Assert.Throws<BackupFailure>(() => RestorePlan.For(
            Install(),
            Manifest() with { Product = "Therapy" },
            "16.10",
            KnownSchemas));

        Assert.Contains("Therapy", failure.Message);
        Assert.Contains("Optical", failure.Message);
    }

    [Fact]
    public void AnArchiveInAFormatThisBuildDoesNotReadIsRefused()
    {
        var failure = Assert.Throws<BackupFailure>(() => RestorePlan.For(
            Install(),
            Manifest() with { ArchiveVersion = BackupManifest.CurrentArchiveVersion + 1 },
            "16.10",
            KnownSchemas));

        Assert.Contains("backup format", failure.Message);
    }

    [Fact]
    public void TheStatementNamesWhatWillBeOverwrittenAndNeverThePassword()
    {
        var statement = RestorePlan.For(Install(), Manifest(), "16.10", KnownSchemas).Statement();

        Assert.Contains("REPLACES", statement);
        Assert.Contains("WILL BE OVERWRITTEN", statement);
        Assert.Contains("optical_main on db.nsail.ar:5432", statement);
        Assert.Contains("/srv/optical/data/keyring", statement);
        Assert.DoesNotContain("s3cret", statement);
    }

    [Fact]
    public void TheStatementSaysWhenTheArchiveCarriesNoKeyRing()
    {
        var statement = RestorePlan.For(Install(), Manifest() with { HasKeyRing = false }, "16.10", KnownSchemas).Statement();

        Assert.Contains("sealed secrets will stay unreadable", statement);
    }

    static InstallDescriptor Install()
    {
        return new InstallDescriptor
        {
            Product = "Optical",
            ConnectionString = InstallConnectionString,
            SchemaVersion = KnownSchemas[^1],
            KeyRingPath = "/srv/optical/data/keyring"
        };
    }

    static BackupManifest Manifest()
    {
        return new BackupManifest
        {
            Product = "Optical",
            Database = "optical_main",
            PostgresVersion = "16.10",
            SchemaVersion = KnownSchemas[^1],
            TakenAtUtc = new DateTime(2026, 8, 27, 10, 0, 0, DateTimeKind.Utc),
            HasKeyRing = true
        };
    }
}
