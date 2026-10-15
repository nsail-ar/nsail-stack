// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using NSail.Mapping.Annotations;

namespace NSail.Data.Tests.Mapping;

// The root's concurrency token stands for the whole aggregate: an edit that changes only its
// parts is still checked against the version the caller read, and still moves it.
public sealed class AggregateTokenTests
{
    static readonly Guid BoardId = Guid.NewGuid();
    static readonly Guid FirstCardId = Guid.NewGuid();

    [Fact]
    public async Task an_edit_of_the_parts_alone_moves_the_root_token()
    {
        await using var store = await Seeded();

        await using (var db = store.Fresh())
        {
            await MapperStore<BoardDbContext>.MapperOver(db).Map<BoardDto, Board>(Read(1, "first", "second"));
            await db.SaveChangesAsync();
        }

        await using var read = store.Fresh();
        var board = await read.Boards.Include(b => b.Cards).SingleAsync();

        Assert.Equal(2, board.Revision);
        Assert.Equal(2, board.Cards.Count);
    }

    [Fact]
    public async Task a_stale_edit_of_the_parts_alone_is_a_conflict()
    {
        await using var store = await Seeded();

        await using (var first = store.Fresh())
        {
            await MapperStore<BoardDbContext>.MapperOver(first).Map<BoardDto, Board>(Read(1, "first", "added"));
            await first.SaveChangesAsync();
        }

        await using var second = store.Fresh();

        await MapperStore<BoardDbContext>.MapperOver(second).Map<BoardDto, Board>(Read(1, "renamed"));

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
    }

    [Fact]
    public async Task a_run_that_changes_nothing_writes_nothing()
    {
        await using var store = await Seeded();

        await using (var db = store.Fresh())
        {
            await MapperStore<BoardDbContext>.MapperOver(db).Map<BoardDto, Board>(Read(1, "first"));

            Assert.False(db.ChangeTracker.HasChanges());
        }

        await using var read = store.Fresh();

        Assert.Equal(1, (await read.Boards.SingleAsync()).Revision);
    }

    [Fact]
    public async Task a_root_with_no_token_is_not_written_for_its_parts()
    {
        await using var store = await MapperStore<BoardDbContext>.Create();

        store.Db.Shelves.Add(new Shelf { Id = BoardId, Name = "shelf", Books = [new Book { Id = FirstCardId, Title = "first" }] });
        await store.Db.SaveChangesAsync();

        await using var db = store.Fresh();

        await MapperStore<BoardDbContext>.MapperOver(db).Map<ShelfDto, Shelf>(new ShelfDto
        {
            Id = BoardId,
            Name = "shelf",
            Books = [new BookDto { Id = FirstCardId, Title = "renamed" }],
        });

        Assert.Equal(EntityState.Unchanged, db.Entry(await db.Shelves.SingleAsync()).State);
    }

    static async Task<MapperStore<BoardDbContext>> Seeded()
    {
        var store = await MapperStore<BoardDbContext>.Create();

        store.Db.Boards.Add(new Board { Id = BoardId, Name = "board", Cards = [new Card { Id = FirstCardId, Title = "first" }] });
        await store.Db.SaveChangesAsync();

        return store;
    }

    // What a screen sends back: the board as it read it at a revision, with its whole set of
    // cards — the first one under its key, any other as new.
    static BoardDto Read(int revision, string firstTitle, params string[] added)
    {
        return new BoardDto
        {
            Id = BoardId,
            Revision = revision,
            Name = "board",
            Cards = [new CardDto { Id = FirstCardId, Title = firstTitle }, .. added.Select(title => new CardDto { Title = title })],
        };
    }

    // A store's row version, as Postgres's xmin behaves: every UPDATE of the row moves it.
    public sealed class BoardDbContext(DbContextOptions<BoardDbContext> options) : DbContext(options)
    {
        public DbSet<Board> Boards => Set<Board>();

        public DbSet<Shelf> Shelves => Set<Shelf>();

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            foreach (var entry in ChangeTracker.Entries<Board>())
            {
                if (entry.State == EntityState.Added)
                {
                    entry.Entity.Revision = 1;
                }
                else if (entry.State == EntityState.Modified)
                {
                    entry.Entity.Revision++;
                }
            }

            return base.SaveChangesAsync(cancellationToken);
        }
    }

    [MapFrom(typeof(BoardDto))]
    public class Board
    {
        public Guid Id { get; set; }

        [ConcurrencyCheck]
        public int Revision { get; set; }

        public string Name { get; set; } = string.Empty;

        [Composition]
        public List<Card> Cards { get; set; } = [];
    }

    [MapFrom(typeof(CardDto))]
    public class Card
    {
        public Guid Id { get; set; }

        public Guid BoardId { get; set; }

        [Parent]
        public Board? Board { get; set; }

        public string Title { get; set; } = string.Empty;
    }

    public class BoardDto
    {
        public Guid Id { get; set; }

        public int Revision { get; set; }

        public string Name { get; set; } = string.Empty;

        public List<CardDto> Cards { get; set; } = [];
    }

    public class CardDto
    {
        public Guid Id { get; set; }

        public string Title { get; set; } = string.Empty;
    }

    [MapFrom(typeof(ShelfDto))]
    public class Shelf
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;

        [Composition]
        public List<Book> Books { get; set; } = [];
    }

    [MapFrom(typeof(BookDto))]
    public class Book
    {
        public Guid Id { get; set; }

        public Guid ShelfId { get; set; }

        public string Title { get; set; } = string.Empty;
    }

    public class ShelfDto
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public List<BookDto> Books { get; set; } = [];
    }

    public class BookDto
    {
        public Guid Id { get; set; }

        public string Title { get; set; } = string.Empty;
    }
}
