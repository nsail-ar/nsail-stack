// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.EntityFrameworkCore;
using NSail.Data;

namespace NSail.Sample;

public class SampleDbContext : DbContext, ITenanted, IHasOrgScope
{
    readonly IEnumerable<IDbContextSetup> _setups;
    readonly TenancyProvider _tenancy;
    readonly OrgScopeProvider _orgs;

    public SampleDbContext(
        DbContextOptions<SampleDbContext> options,
        IEnumerable<IDbContextSetup> setups,
        TenancyProvider? tenancy = null,
        OrgScopeProvider? orgs = null)
        : base(options)
    {
        _setups = setups;
        _tenancy = tenancy ?? TenancyProvider.Install;
        _orgs = orgs ?? OrgScopeProvider.Ambient;
    }

    public Guid? TenantId
    {
        get { return _tenancy.RowKey; }
    }

    public OrgScope OrgScope
    {
        get { return _orgs.Current; }
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.ApplyTenancy();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplySetups(this, _setups);
    }
}
