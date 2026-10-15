// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.EntityFrameworkCore;
using NSail.Data.Testing;
using NSail.Mapping.Annotations;

namespace NSail.Data.Tests.Mapping;

// The same rule on the store it exists for: Postgres moves a row's xmin only when that row is
// written, so an edit of the parts alone must write the root for IVersioned to hold.
public sealed class AggregateTokenPostgresTests
{
    [Fact]
    public async Task a_stale_edit_of_the_parts_alone_is_a_conflict_on_xmin()
    {
        await using var database = new TestDatabase("aggregatetoken", connection => new ListContext(connection));
        await database.Reserve();

        var listId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        uint read;

        await using (var seed = new ListContext(database.ConnectionString))
        {
            await seed.Database.EnsureCreatedAsync();

            var list = new TodoList { Id = listId, Name = "list", Items = [new TodoItem { Id = itemId, Title = "first" }] };

            seed.Lists.Add(list);
            await seed.SaveChangesAsync();
            read = list.Version;
        }

        await using (var first = new ListContext(database.ConnectionString))
        {
            await MapperStore<ListContext>.MapperOver(first).Map<TodoListDto, TodoList>(new TodoListDto
            {
                Id = listId,
                Version = read,
                Name = "list",
                Items = [new TodoItemDto { Id = itemId, Title = "first" }, new TodoItemDto { Title = "added" }],
            });

            await first.SaveChangesAsync();
        }

        await using (var check = new ListContext(database.ConnectionString))
        {
            Assert.NotEqual(read, (await check.Lists.SingleAsync()).Version);
        }

        await using var second = new ListContext(database.ConnectionString);

        await MapperStore<ListContext>.MapperOver(second).Map<TodoListDto, TodoList>(new TodoListDto
        {
            Id = listId,
            Version = read,
            Name = "list",
            Items = [new TodoItemDto { Id = itemId, Title = "renamed" }],
        });

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
    }

    public sealed class ListContext : DbContext
    {
        readonly string _connection;

        public ListContext(string connection)
        {
            _connection = connection;
        }

        public ListContext(DbContextOptions<ListContext> options)
            : base(options)
        {
            _connection = string.Empty;
        }

        public DbSet<TodoList> Lists => Set<TodoList>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseNpgsql(_connection);
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TodoList>().Property(l => l.Version).IsRowVersion();
        }
    }

    [MapFrom(typeof(TodoListDto))]
    public class TodoList : IVersioned
    {
        public Guid Id { get; set; }

        public uint Version { get; set; }

        public string Name { get; set; } = string.Empty;

        [Composition]
        public List<TodoItem> Items { get; set; } = [];
    }

    [MapFrom(typeof(TodoItemDto))]
    public class TodoItem
    {
        public Guid Id { get; set; }

        public Guid TodoListId { get; set; }

        public string Title { get; set; } = string.Empty;
    }

    public class TodoListDto
    {
        public Guid Id { get; set; }

        public uint Version { get; set; }

        public string Name { get; set; } = string.Empty;

        public List<TodoItemDto> Items { get; set; } = [];
    }

    public class TodoItemDto
    {
        public Guid Id { get; set; }

        public string Title { get; set; } = string.Empty;
    }
}
