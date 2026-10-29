// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.EntityFrameworkCore;

namespace NSail.Data.Tests;

public class TenantPolicyTests
{
    [Fact]
    public void TheMarkerDecidesWhichTablesOweAPolicy()
    {
        using var context = Model();

        Assert.Equal(["AppliedSeedSteps", "Notes"], TenantPolicies.Tables(context.Model));
    }

    // The one entity where the marker and the column disagree, and the reason the sweep asks the
    // marker: TenantStates carries the tenant it is ABOUT, which is not the tenant a row belongs
    // to, so a policy over it would hide the install's own record of its customers from the
    // install.
    [Fact]
    public void ARowCarryingATenantOfItsOwnOwesNone()
    {
        using var context = Model();

        Assert.NotNull(context.Model.FindEntityType(typeof(TenantState))!.FindProperty(nameof(TenantState.TenantId)));
        Assert.DoesNotContain("TenantStates", TenantPolicies.Tables(context.Model));
    }

    // The third of the three tables the Stack owns, and the only one the install writes: the keys
    // this build declares are the install's own and no tenant holds a copy, so it takes no tenant
    // column at all — and a policy over it would hide from the install what the install itself
    // published.
    [Fact]
    public void ThePublishedKeysOweNoneAndTakeNoTenantColumn()
    {
        using var context = Model();

        Assert.Null(context.Model.FindEntityType(typeof(DeclaredFeature))!.FindProperty("TenantId"));
        Assert.DoesNotContain("DeclaredFeatures", TenantPolicies.Tables(context.Model));
    }

    // An owned type's columns are its owner's row, which already carries the tenant, so there is
    // no second table to wall — and that premise is the whole reason it owes no policy.
    [Fact]
    public void AnOwnedTypeSharingItsOwnersRowOwesNone()
    {
        using var context = Model();

        var colours = context.Model.FindEntityType(typeof(PolicyColours))!;

        Assert.Equal("Notes", colours.GetTableName());
        Assert.Null(colours.FindProperty("TenantId"));
        Assert.Equal(["AppliedSeedSteps", "Notes"], TenantPolicies.Tables(context.Model));
    }

    // And where the premise is false the model is refused, because nothing else can catch it: a
    // table of the owned type's own takes no tenant column and no filter, the chain writes it no
    // policy, and the sweep — which reads the model for the tables that OWE one — calls the
    // install clean.
    [Fact]
    public void AnOwnedTypeMappedToATableOfItsOwnIsRefused()
    {
        using var context = new SplitDbContext(
            new DbContextOptionsBuilder<SplitDbContext>()
                .UseNpgsql("Host=localhost;Database=none;Username=postgres")
                .Options);

        var refusal = Assert.Throws<InvalidOperationException>(() => context.Model);

        Assert.Contains("\"Colours\"", refusal.Message, StringComparison.Ordinal);
        Assert.Contains("behind neither wall", refusal.Message, StringComparison.Ordinal);
    }

    // And where the row shared is the INSTALL's, the second table is not that shape at all: the
    // same rows for every tenant, which is what the marker means. The refusal asks the marker on
    // the ROOT of the chain, because an owned collection reaches it through a type that carries
    // none and a TPT leaf through its base — and it refuses in every mode, so a model refused
    // here would not start under None either, where there is no second wall and no tenants.
    [Theory]
    [InlineData("KindLabels")]
    [InlineData("SeasonalKinds")]
    public void ATableHangingOffAnInstallScopedRootIsNotRefusedAndOwesNoPolicy(string table)
    {
        using var context = new InstallSplitDbContext(
            new DbContextOptionsBuilder<InstallSplitDbContext>()
                .UseNpgsql("Host=localhost;Database=none;Username=postgres")
                .Options);

        var entity = Assert.Single(context.Model.GetEntityTypes(), type => type.GetTableName() == table);

        Assert.Null(entity.FindProperty("TenantId"));
        Assert.Equal(["AppliedSeedSteps", "Notes"], TenantPolicies.Tables(context.Model));
    }

    // Raw SQL handed to migrationBuilder.Sql carries no terminator of its own and the generated
    // script concatenates commands without adding one, so a statement missing its semicolon
    // swallows whatever EF wrote next (MigrationScriptTests reads the chain as that one script).
    [Theory]
    [InlineData("ENABLE ROW LEVEL SECURITY;")]
    [InlineData("FORCE ROW LEVEL SECURITY;")]
    [InlineData("CREATE POLICY \"nsail_tenant\"")]
    public void EveryStatementOfTheWallIsTerminated(string statement)
    {
        var ddl = TenantPolicies.Create("Notes");

        Assert.Contains(statement, ddl, StringComparison.Ordinal);
        Assert.EndsWith(";", ddl.TrimEnd(), StringComparison.Ordinal);
        Assert.All(
            ddl.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            line => Assert.EndsWith(";", line, StringComparison.Ordinal));
    }

    // nullif before the cast: a session that never wrote the setting reads NULL, one whose
    // pooled connection was reset reads the empty string, and '' cast to uuid is an error rather
    // than a comparison nothing matches.
    [Fact]
    public void ThePredicateReadsAnUnwrittenSettingAsNothing()
    {
        Assert.Contains(
            $"nullif(current_setting('{TenantPolicies.Setting}', true), '')::uuid",
            TenantPolicies.Create("Notes"),
            StringComparison.Ordinal);
    }

    static PolicyDbContext Model()
    {
        var options = new DbContextOptionsBuilder<PolicyDbContext>()
            .UseNpgsql("Host=localhost;Database=none;Username=postgres")
            .Options;

        return new PolicyDbContext(options);
    }
}

public sealed class PolicyDbContext : DbContext, ITenanted, IHasOrgScope
{
    public PolicyDbContext(DbContextOptions<PolicyDbContext> options)
        : base(options)
    {
    }

    public Guid? TenantId
    {
        get { return Guid.Empty; }
    }

    public OrgScope OrgScope
    {
        get { return OrgScope.Everywhere; }
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.ApplyTenancy();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplySetups(this, [new PolicySetup()]);
    }
}

sealed class PolicySetup : IDbContextSetup
{
    public void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PolicyNote>(note =>
        {
            note.ToTable("Notes");
            note.HasKey(entity => entity.Id);
            note.OwnsOne(entity => entity.Colours);
        });

        modelBuilder.Entity<PolicyNoteKind>(kind =>
        {
            kind.ToTable("NoteKinds");
            kind.HasKey(entity => entity.Id);
        });
    }
}

// The same model again with its owned type told to live in a table of its own, which is the one
// shape the tenant pass refuses.
public sealed class SplitDbContext : DbContext, ITenanted, IHasOrgScope
{
    public SplitDbContext(DbContextOptions<SplitDbContext> options)
        : base(options)
    {
    }

    public Guid? TenantId
    {
        get { return Guid.Empty; }
    }

    public OrgScope OrgScope
    {
        get { return OrgScope.Everywhere; }
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.ApplyTenancy();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplySetups(this, [new SplitSetup()]);
    }
}

sealed class SplitSetup : IDbContextSetup
{
    public void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PolicyNote>(note =>
        {
            note.ToTable("Notes");
            note.HasKey(entity => entity.Id);
            note.OwnsOne(entity => entity.Colours, colours => colours.ToTable("Colours"));
        });
    }
}

// The install's own reference data wearing the two shapes that hang a second table off it: an
// owned collection, which EF maps to a table of its own by default, and a hierarchy told TPT.
public sealed class InstallSplitDbContext : DbContext, ITenanted, IHasOrgScope
{
    public InstallSplitDbContext(DbContextOptions<InstallSplitDbContext> options)
        : base(options)
    {
    }

    public Guid? TenantId
    {
        get { return Guid.Empty; }
    }

    public OrgScope OrgScope
    {
        get { return OrgScope.Everywhere; }
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.ApplyTenancy();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplySetups(this, [new InstallSplitSetup()]);
    }
}

sealed class InstallSplitSetup : IDbContextSetup
{
    public void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PolicyNote>(note =>
        {
            note.ToTable("Notes");
            note.HasKey(entity => entity.Id);
            note.OwnsOne(entity => entity.Colours);
        });

        modelBuilder.Entity<PolicyLabelledKind>(kind =>
        {
            kind.ToTable("LabelledKinds");
            kind.HasKey(entity => entity.Id);
            kind.OwnsMany(entity => entity.Labels, labels => labels.ToTable("KindLabels"));
        });

        modelBuilder.Entity<PolicySeasonalKind>(kind => kind.ToTable("SeasonalKinds"));
    }
}

public sealed class PolicyNote
{
    public Guid Id { get; set; }

    public PolicyColours Colours { get; set; } = new();
}

public sealed class PolicyColours
{
    public string Ink { get; set; } = string.Empty;
}

public sealed class PolicyNoteKind : IInstallScoped
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;
}

public class PolicyLabelledKind : IInstallScoped
{
    public Guid Id { get; set; }

    public List<PolicyLabel> Labels { get; } = [];
}

public sealed class PolicyLabel
{
    public string Text { get; set; } = string.Empty;
}

public sealed class PolicySeasonalKind : PolicyLabelledKind
{
    public string Season { get; set; } = string.Empty;
}
