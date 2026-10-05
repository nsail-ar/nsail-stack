// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace NSail.Data.Tests;

// CreatedAt and UpdatedAt are the persistence layer's, so a handler never says them. These pin
// the rule against a context built the way a kit's is: the interceptor from OnConfiguring.
public sealed class AuditStampTests : IAsyncLifetime
{
    SqliteConnection _connection = null!;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("DataSource=:memory:");

        await _connection.OpenAsync();

        await using var db = Context();

        await db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task AnAddedRowGetsBothColumnsFromTheSave()
    {
        var before = DateTime.UtcNow;

        await using var db = Context();

        var note = new Note { Id = Guid.NewGuid(), Text = "first" };

        db.Add(note);

        await db.SaveChangesAsync();

        Assert.InRange(note.CreatedAt, before, DateTime.UtcNow);
        Assert.Equal(note.CreatedAt, note.UpdatedAt);
    }

    [Fact]
    public async Task AValueSetBeforeTheAddIsKept()
    {
        var created = new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc);

        await using var db = Context();

        var note = new Note { Id = Guid.NewGuid(), Text = "imported", CreatedAt = created, UpdatedAt = created };

        db.Add(note);

        await db.SaveChangesAsync();

        Assert.Equal(created, note.CreatedAt);
        Assert.Equal(created, note.UpdatedAt);
    }

    [Fact]
    public async Task AChangedRowMovesUpdatedAtAndNeverCreatedAt()
    {
        var id = Guid.NewGuid();

        await using (var first = Context())
        {
            first.Add(new Note { Id = id, Text = "before" });

            await first.SaveChangesAsync();
        }

        await using var db = Context();

        var note = await db.Set<Note>().SingleAsync(n => n.Id == id);
        var created = note.CreatedAt;
        var updated = note.UpdatedAt;

        await Task.Delay(20);

        note.Text = "after";
        note.CreatedAt = DateTime.UtcNow.AddYears(-5);

        await db.SaveChangesAsync();

        await using var cold = Context();

        var stored = await cold.Set<Note>().SingleAsync(n => n.Id == id);

        Assert.Equal(created, stored.CreatedAt, TimeSpan.FromMilliseconds(1));
        Assert.True(stored.UpdatedAt > updated);
    }

    [Fact]
    public async Task ARowNothingChangedIsNotStamped()
    {
        var id = Guid.NewGuid();

        await using (var first = Context())
        {
            first.Add(new Note { Id = id, Text = "same" });

            await first.SaveChangesAsync();
        }

        await using var db = Context();

        var note = await db.Set<Note>().SingleAsync(n => n.Id == id);
        var updated = note.UpdatedAt;

        await Task.Delay(20);

        note.Text = "same";

        await db.SaveChangesAsync();

        await using var cold = Context();

        Assert.Equal(updated, (await cold.Set<Note>().SingleAsync(n => n.Id == id)).UpdatedAt, TimeSpan.FromMilliseconds(1));
    }

    AuditedContext Context()
    {
        return new AuditedContext(new DbContextOptionsBuilder<AuditedContext>().UseSqlite(_connection).Options);
    }

    sealed class Note : IAudited
    {
        public Guid Id { get; set; }

        public string Text { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }
    }

    sealed class AuditedContext : DbContext
    {
        public AuditedContext(DbContextOptions<AuditedContext> options)
            : base(options)
        {
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseAuditStamps();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Note>();
        }
    }
}
