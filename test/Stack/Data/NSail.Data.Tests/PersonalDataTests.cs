// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.EntityFrameworkCore;
using NSail.Data.Testing;
using NSail.Privacy;
using Npgsql;

namespace NSail.Data.Tests;

// The carriage, end to end on the store it exists for: a declaration on a property has to reach
// Postgres's own col_description, because that catalog read is all a program outside the app has
// — the fleet runner that copies a tenant's rows knows no product, no kit and no entity. A model
// assertion alone would pass over a provider that drops the comment on the floor.
public sealed class PersonalDataTests : IClassFixture<PersonalSchema>
{
    readonly PersonalSchema _schema;

    public PersonalDataTests(PersonalSchema schema)
    {
        _schema = schema;
    }

    [Fact]
    public void A_declared_column_carries_its_kind_as_the_columns_comment()
    {
        var comments = _schema.Comments("People");

        Assert.Equal("personal:name", comments["FullName"]);
        Assert.Equal("personal:email", comments["Email"]);
        Assert.Equal("personal:free-text", comments["Notes"]);
    }

    // Allowed on any property, demanded on text: a date of birth identifies a person as plainly
    // as a name does, and the runner has to be told so to fake it.
    [Fact]
    public void A_declaration_on_a_column_that_is_not_text_rides_the_same_way()
    {
        Assert.Equal("personal:birth-date", _schema.Comments("People")["BornOn"]);
    }

    // Nothing is written for "not personal": a column with no comment holds no personal datum,
    // and ~250 no-op comments would be schema every reader has to discount.
    [Fact]
    public void A_column_declared_not_personal_carries_no_comment()
    {
        var comments = _schema.Comments("People");

        Assert.Null(comments["Code"]);
        Assert.Null(comments["TenantId"]);
    }

    // The declaration is inherited, so a base class answering for its own property answers for
    // every entity built on it.
    [Fact]
    public void A_declaration_on_a_base_property_reaches_the_entitys_own_table()
    {
        var comments = _schema.Comments("Pets");

        Assert.Equal("personal:name", comments["FullName"]);
        Assert.Null(comments["Species"]);
    }
}

// One database for the whole class: the subject is a schema, written once and read by every
// assertion, and a database per fact would spend four of the test gate's permits saying the same
// CREATE TABLE.
public sealed class PersonalSchema : IAsyncLifetime
{
    readonly Dictionary<string, Dictionary<string, string?>> _tables = new(StringComparer.Ordinal);

    TestDatabase _database = null!;

    public async Task InitializeAsync()
    {
        _database = new TestDatabase("personaldata", connection => new PersonalDbContext(connection));

        await _database.Reserve();

        await using (var schema = new PersonalDbContext(_database.ConnectionString))
        {
            await schema.Database.EnsureCreatedAsync();
        }

        foreach (var table in new[] { "People", "Pets" })
        {
            _tables[table] = await Read(_database.ConnectionString, table);
        }
    }

    public async Task DisposeAsync()
    {
        await _database.DisposeAsync();
    }

    public Dictionary<string, string?> Comments(string table)
    {
        return _tables[table];
    }

    static async Task<Dictionary<string, string?>> Read(string connectionString, string table)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();

        command.CommandText = """
            select attribute.attname, col_description(attribute.attrelid, attribute.attnum)
            from pg_attribute attribute
            join pg_class relation on relation.oid = attribute.attrelid
            where relation.relname = @table and attribute.attnum > 0 and not attribute.attisdropped
            """;
        command.Parameters.AddWithValue("table", table);

        var comments = new Dictionary<string, string?>(StringComparer.Ordinal);

        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            comments[reader.GetString(0)] = await reader.IsDBNullAsync(1) ? null : reader.GetString(1);
        }

        return comments;
    }
}

public abstract class NamedByAPerson
{
    public Guid Id { get; set; }

    [Personal(PersonalData.Name)]
    public string FullName { get; set; } = string.Empty;
}

public sealed class Person : NamedByAPerson
{
    [NotPersonal]
    public string Code { get; set; } = string.Empty;

    [Personal(PersonalData.Email)]
    public string? Email { get; set; }

    [Personal(PersonalData.FreeText)]
    public string? Notes { get; set; }

    [Personal(PersonalData.BirthDate)]
    public DateOnly? BornOn { get; set; }
}

public sealed class Pet : NamedByAPerson
{
    [NotPersonal]
    public string Species { get; set; } = string.Empty;
}

public sealed class PersonalDbContext : DbContext, ITenanted, IHasOrgScope
{
    readonly string _connection;

    public PersonalDbContext(string connection)
    {
        _connection = connection;
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

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseNpgsql(_connection);
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplySetups(this, [new PersonalSetup()]);
    }
}

sealed class PersonalSetup : IDbContextSetup
{
    public void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Person>(person =>
        {
            person.ToTable("People");
            person.HasKey(entity => entity.Id);
        });

        modelBuilder.Entity<Pet>(pet =>
        {
            pet.ToTable("Pets");
            pet.HasKey(entity => entity.Id);
        });
    }
}
