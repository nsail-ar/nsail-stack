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
