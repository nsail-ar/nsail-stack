// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.EntityFrameworkCore;

namespace NSail.Data.Tests;

/// <summary>Which columns the model-wide pass gives the case-insensitive collation to
/// (<c>ApplySetups</c>, data.md): every text column, whoever named its type, and nothing that
/// is not text. A column that silently loses the rule stops a <c>Contains</c> search matching
/// with nothing on the entity to say why, which is why the exception is a roster of types that
/// cannot carry a collation at all and not "whoever named one".</summary>
public class CaseInsensitiveTextTests
{
    [Fact]
    public void APlainTextColumnCarriesTheCollation()
    {
        Assert.Contains(Collations.CaseInsensitive, Column(nameof(TextNote.Title)), StringComparison.Ordinal);
    }

    /// <summary>A named type is not an opt-out: `text`, `varchar(120)` and `citext` are the plain
    /// text this rule is about, and a column that lost it would stop a `Contains` search matching
    /// with nothing on the entity to say why.</summary>
    [Fact]
    public void ATextColumnWhoseTypeTheModelNamedCarriesItToo()
    {
        Assert.Contains(Collations.CaseInsensitive, Column(nameof(TextNote.Code)), StringComparison.Ordinal);
        Assert.Contains(Collations.CaseInsensitive, Column(nameof(TextNote.Label)), StringComparison.Ordinal);
    }

    /// <summary>A string stored as something that is not text takes none: Postgres refuses a
    /// collation there and the migration creating the column would not apply.</summary>
    [Fact]
    public void AColumnThatIsNotTextCarriesNone()
    {
        Assert.DoesNotContain("COLLATE", Column(nameof(TextNote.Bag)), StringComparison.Ordinal);
    }

    /// <summary>The entitlements bag is the Stack's own instance of it, beside the plan's word
    /// that goes through the same pass.</summary>
    [Fact]
    public void TheInstallsRecordAboutATenantIsTextExceptItsBag()
    {
        Assert.Contains(Collations.CaseInsensitive, Column(nameof(TenantState.Plan)), StringComparison.Ordinal);
        Assert.DoesNotContain("COLLATE", Column(nameof(TenantState.Entitlements)), StringComparison.Ordinal);
    }

    [Fact]
    public void AValueThatMustCompareByteForByteOptsOut()
    {
        Assert.DoesNotContain("COLLATE", Column(nameof(TextNote.Secret)), StringComparison.Ordinal);
    }

    // Read off the DDL the provider would run rather than off the model: what a wrong answer
    // costs is a CREATE Postgres refuses, and the runtime model carries no collation at all.
    static string Column(string name)
    {
        using var context = Model();

        var line = context.Database
            .GenerateCreateScript()
            .Split('\n')
            .FirstOrDefault(statement => statement.Contains($"\"{name}\" ", StringComparison.Ordinal));

        Assert.NotNull(line);

        return line;
    }

    static TextDbContext Model()
    {
        var options = new DbContextOptionsBuilder<TextDbContext>()
            .UseNpgsql("Host=localhost;Database=none;Username=postgres")
            .Options;

        return new TextDbContext(options);
    }
}

public sealed class TextDbContext : DbContext, ITenanted, IHasOrgScope
{
    public TextDbContext(DbContextOptions<TextDbContext> options)
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
        modelBuilder.ApplySetups(this, [new TextSetup()]);
    }
}

sealed class TextSetup : IDbContextSetup
{
    public void Configure(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<TextNote>(note =>
        {
            note.ToTable("TextNotes");
            note.HasKey(entity => entity.Id);

            note.Property(entity => entity.Code)
                .HasColumnType("varchar(120)");

            note.Property(entity => entity.Label)
                .HasColumnType("text");

            note.Property(entity => entity.Bag)
                .HasColumnType("jsonb");
        });
    }
}

public sealed class TextNote
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string? Bag { get; set; }

    [CaseSensitive]
    public string Secret { get; set; } = string.Empty;
}
