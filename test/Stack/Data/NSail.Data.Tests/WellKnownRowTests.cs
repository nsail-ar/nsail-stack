// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.Data.Tests;

// The seam that lets a frozen id stay a row's NAME. Everything the tree names by constant — a
// seeded Role, the account a setting defaults to — is the install's row under a mode whose rows
// are the install's, and the tenant's own twin under one whose rows are a tenant's. What these
// pin is the property the whole move rests on: the mapping is TOTAL and MODE-INVARIANT, so no
// call site has to know which mode it is running under, and nothing outside the declared set is
// touched at all.
public class WellKnownRowTests
{
    const string Slug = "lumina";

    static readonly Guid Frozen = new("c2222222-2222-2222-2222-222222222222");
    static readonly Guid Chosen = new("11111111-2222-3333-4444-555555555555");
    static readonly Guid Shared = new("c4000000-0000-0000-0000-000000000001");

    [Theory]
    [InlineData(TenancyMode.None)]
    [InlineData(TenancyMode.MultiDb)]
    public void WhereTheRowsAreTheInstallsTheTranslationIsTheIdentity(TenancyMode mode)
    {
        var tenancy = Provider(mode, Tenant.For(Slug, "optical_sa1_lumina"));

        Assert.Equal(Frozen, tenancy.Row(Frozen));
        Assert.Equal(Chosen, tenancy.Row(Chosen));
    }

    // The other half of mode-invariance, and the reason the bootstrap and this seam must be one
    // function called twice: the id a handler resolves is the id the first touch planted, or the
    // handler is naming a row that does not exist.
    [Fact]
    public void WhereTheRowsAreTheTenantsTheTranslationIsTheBootstrapsOwnDerivation()
    {
        var tenancy = Provider(TenancyMode.SingleDb, Tenant.For(Slug));

        Assert.Equal(TenantKey.For(Slug, Frozen), tenancy.Row(Frozen));
    }

    [Fact]
    public void TwoTenantsGetTwoTwinsOfTheSameFrozenRow()
    {
        var lumina = Provider(TenancyMode.SingleDb, Tenant.For("lumina"));
        var vision = Provider(TenancyMode.SingleDb, Tenant.For("vision"));

        Assert.NotEqual(lumina.Row(Frozen), vision.Row(Frozen));
    }

    // The set is what makes the seam safe to point at a value that may not be frozen at all: a
    // stored setting holds either the install's constant or a row the tenant picked itself, and
    // translating the second would send the handler at nobody's row.
    [Fact]
    public void AnIdOutsideTheDeclaredSetPassesThroughUntouched()
    {
        var tenancy = Provider(TenancyMode.SingleDb, Tenant.For(Slug));

        Assert.Equal(Chosen, tenancy.Row(Chosen));
    }

    // Same answer as every other read in this seam gives an unresolved scope: the frozen id is
    // the install's row, which carries no tenant and which the filter hides. Reads nothing.
    [Fact]
    public void AScopeThatResolvedNoTenantAnswersTheFrozenId()
    {
        var tenancy = Provider(TenancyMode.SingleDb, tenant: null);

        Assert.Equal(Frozen, tenancy.Row(Frozen));
    }

    [Fact]
    public void AnInstallThatDeclaredNothingTranslatesNothing()
    {
        var tenancy = new StubTenancyProvider(
            new TenancyOptions { Mode = TenancyMode.SingleDb },
            WellKnownRows.None,
            InstallScopedRows.None,
            Tenant.For(Slug));

        Assert.Equal(Frozen, tenancy.Row(Frozen));
    }

    // The other half of the same seam, for the ids a DOCUMENT carries: a starter pack names rows
    // like any other caller, and it also creates rows of its own. What these pin is that the only
    // id such a document keeps is one every scope shares — everything else, named or created,
    // becomes the importing scope's own, which is what lets one document be imported by every
    // tenant of one database without two of them colliding on a primary key.
    [Theory]
    [InlineData(TenancyMode.None)]
    [InlineData(TenancyMode.MultiDb)]
    public void WhereTheRowsAreTheInstallsADocumentsIdsAreItsOwn(TenancyMode mode)
    {
        var tenancy = Provider(mode, Tenant.For(Slug, "optical_sa1_lumina"));

        Assert.Equal(Frozen, tenancy.Owned(Frozen));
        Assert.Equal(Chosen, tenancy.Owned(Chosen));
        Assert.Equal(Shared, tenancy.Owned(Shared));
    }

    [Fact]
    public void ARowTheInstallSharesIsTheSameRowForEveryTenant()
    {
        var lumina = Provider(TenancyMode.SingleDb, Tenant.For("lumina"));
        var vision = Provider(TenancyMode.SingleDb, Tenant.For("vision"));

        Assert.Equal(Shared, lumina.Owned(Shared));
        Assert.Equal(Shared, vision.Owned(Shared));
    }

    [Fact]
    public void AnIdTheInstallDoesNotShareIsDerivedPerTenant()
    {
        var lumina = Provider(TenancyMode.SingleDb, Tenant.For("lumina"));
        var vision = Provider(TenancyMode.SingleDb, Tenant.For("vision"));

        Assert.Equal(TenantKey.For("lumina", Chosen), lumina.Owned(Chosen));
        Assert.NotEqual(lumina.Owned(Chosen), vision.Owned(Chosen));
    }

    // The two doors answer one row: a document naming a frozen id and a handler naming the same
    // constant must land on the same row, or the pack's memberships point at a role nobody has.
    [Fact]
    public void ADocumentAndAHandlerNamingTheSameFrozenRowAnswerTheSameId()
    {
        var tenancy = Provider(TenancyMode.SingleDb, Tenant.For(Slug));

        Assert.Equal(tenancy.Row(Frozen), tenancy.Owned(Frozen));
    }

    [Fact]
    public void AScopeThatResolvedNoTenantAnswersADocumentsIdUntouched()
    {
        var tenancy = Provider(TenancyMode.SingleDb, tenant: null);

        Assert.Equal(Chosen, tenancy.Owned(Chosen));
    }

    static TenancyProvider Provider(TenancyMode mode, Tenant? tenant)
    {
        return new StubTenancyProvider(
            new TenancyOptions { Mode = mode, Product = "optical", Cell = "sa1" },
            new WellKnownRows([new DeclaredRowsStub(Frozen)]),
            new InstallScopedRows([new DeclaredRowsStub(Shared)]),
            tenant);
    }

    sealed class DeclaredRowsStub : IWellKnownRows, IInstallScopedRows
    {
        public DeclaredRowsStub(params Guid[] ids)
        {
            Ids = ids;
        }

        public IEnumerable<Guid> Ids { get; }
    }

    sealed class StubTenancyProvider : TenancyProvider
    {
        readonly Tenant? _tenant;

        public StubTenancyProvider(TenancyOptions tenancy, WellKnownRows rows, InstallScopedRows shared, Tenant? tenant)
            : base(tenancy, rows, shared)
        {
            _tenant = tenant;
        }

        public override Tenant Current
        {
            get { return _tenant ?? Tenant.None; }
        }
    }
}
