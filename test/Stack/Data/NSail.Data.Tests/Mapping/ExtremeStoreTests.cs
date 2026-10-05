// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using NSail.Mapping.Annotations;

namespace NSail.Data.Tests.Mapping;

// The mapper against a real store at its limits: every verb at every level of one graph in a
// single save, a tree that owns itself, composite keys, one row reached from four places, a
// token gone stale, a message sent twice, a cancelled run and a collection in the thousands.
// Each graph is read back from a cold context: what counts is what the store holds.
public sealed class ExtremeStoreTests
{
    [Fact]
    public async Task every_verb_at_every_level_in_one_save()
    {
        await using var store = await MapperStore<ExtremeContext>.Create();

        store.Db.Add(new Catalog
        {
            Id = 1,
            Name = "c",
            Sections =
            [
                new Section { Id = 1, Name = "kept", Items = [new SectionItem { Id = 1, Name = "kept" }, new SectionItem { Id = 2, Name = "dropped" }] },
                new Section { Id = 2, Name = "dropped", Items = [new SectionItem { Id = 3, Name = "goes with it" }] },
            ],
        });
        await store.Db.SaveChangesAsync();
        store.Db.ChangeTracker.Clear();

        await store.Mapper.Map<CatalogDto, Catalog>(new CatalogDto
        {
            Id = 1,
            Name = "c2",
            Sections =
            [
                new SectionDto { Id = 1, Name = "kept2", Items = [new SectionItemDto { Id = 1, Name = "kept2" }, new SectionItemDto { Id = 4, Name = "new" }] },
                new SectionDto { Id = 3, Name = "new", Items = [new SectionItemDto { Id = 5, Name = "new" }] },
            ],
        });
        await store.Db.SaveChangesAsync();

        await using var read = store.Fresh();

        var catalog = await read.Catalogs.Include(c => c.Sections!).ThenInclude(s => s.Items).SingleAsync();

        Assert.Equal("c2", catalog.Name);
        Assert.Equal([1, 3], catalog.Sections!.Select(s => s.Id).Order());
        Assert.Equal("kept2", catalog.Sections!.Single(s => s.Id == 1).Name);
        Assert.Equal([1, 4], catalog.Sections!.Single(s => s.Id == 1).Items!.Select(i => i.Id).Order());
        Assert.Equal("kept2", catalog.Sections!.Single(s => s.Id == 1).Items!.Single(i => i.Id == 1).Name);
        Assert.Equal([5], catalog.Sections!.Single(s => s.Id == 3).Items!.Select(i => i.Id));
        Assert.Equal([1, 4, 5], (await read.Set<SectionItem>().Select(i => i.Id).ToListAsync()).Order());
    }

    [Fact]
    public async Task a_tree_that_owns_itself_is_reshaped_level_by_level()
    {
        await using var store = await MapperStore<ExtremeContext>.Create();

        await store.Mapper.Upsert<NodeDto, Node>(Tree(1, "root", Tree(2, "a", Tree(4, "a1"), Tree(5, "a2")), Tree(3, "b", Tree(6, "b1", Tree(7, "b1x")))));
        await store.Db.SaveChangesAsync();
        store.Db.ChangeTracker.Clear();

        // a2 and the whole b branch go, a1 is renamed, a1 grows a child, c arrives.
        await store.Mapper.Map<NodeDto, Node>(Tree(1, "root", Tree(2, "a", Tree(4, "a1*", Tree(8, "a1a"))), Tree(9, "c")));
        await store.Db.SaveChangesAsync();

        await using var read = store.Fresh();

        var all = await read.Nodes.ToListAsync();

        Assert.Equal([1, 2, 4, 8, 9], all.Select(n => n.Id).Order());
        Assert.Equal("a1*", all.Single(n => n.Id == 4).Name);
        Assert.Equal(4, all.Single(n => n.Id == 8).ParentId);
        Assert.Equal(1, all.Single(n => n.Id == 9).ParentId);
    }

    [Fact]
    public async Task composite_keys_merge_by_both_halves()
    {
        await using var store = await MapperStore<ExtremeContext>.Create();

        store.Db.Add(new Shelf { Id = 1, Slots = [new Slot { Row = 1, Column = 1, Label = "old" }, new Slot { Row = 1, Column = 2, Label = "gone" }] });
        await store.Db.SaveChangesAsync();
        store.Db.ChangeTracker.Clear();

        await store.Mapper.Map<ShelfDto, Shelf>(new ShelfDto
        {
            Id = 1,
            Slots = [new SlotDto { Row = 1, Column = 1, Label = "new" }, new SlotDto { Row = 2, Column = 1, Label = "added" }],
        });
        await store.Db.SaveChangesAsync();

        await using var read = store.Fresh();

        var slots = await read.Set<Slot>().OrderBy(s => s.Row).ThenBy(s => s.Column).ToListAsync();

        Assert.Equal([(1, 1, "new"), (2, 1, "added")], slots.Select(s => (s.Row, s.Column, s.Label)));
    }

    [Fact]
    public async Task one_row_reached_from_four_places_is_attached_once()
    {
        await using var store = await MapperStore<ExtremeContext>.Create();

        store.Db.Add(new Tag { Id = 1, Name = "shared" });
        await store.Db.SaveChangesAsync();
        store.Db.ChangeTracker.Clear();

        await store.Mapper.Upsert<CatalogDto, Catalog>(new CatalogDto
        {
            Id = 1,
            Tag = new TagDto { Id = 1 },
            Sections =
            [
                new SectionDto { Id = 1, Tag = new TagDto { Id = 1 }, Items = [new SectionItemDto { Id = 1, Tag = new TagDto { Id = 1 } }] },
                new SectionDto { Id = 2, TagId = 1 },
            ],
        });

        Assert.Single(store.Db.ChangeTracker.Entries<Tag>());

        await store.Db.SaveChangesAsync();

        await using var read = store.Fresh();

        Assert.Equal(1, await read.Set<Tag>().CountAsync());
        Assert.Equal("shared", (await read.Set<Tag>().SingleAsync()).Name);
        Assert.All(await read.Set<Section>().ToListAsync(), s => Assert.Equal(1, s.TagId));
        Assert.Equal(1, (await read.Set<SectionItem>().SingleAsync()).TagId);
    }

    [Fact]
    public async Task a_stale_token_refuses_the_whole_save()
    {
        await using var store = await MapperStore<ExtremeContext>.Create();

        store.Db.Add(new Catalog { Id = 1, Name = "v1", Revision = 1 });
        await store.Db.SaveChangesAsync();
        store.Db.ChangeTracker.Clear();

        // Someone else saved revision 2 after this caller read revision 1.
        await using (var other = store.Fresh())
        {
            var row = await other.Catalogs.SingleAsync();
            row.Revision = 2;
            row.Name = "theirs";
            await other.SaveChangesAsync();
        }

        await store.Mapper.Map<CatalogDto, Catalog>(new CatalogDto { Id = 1, Name = "mine", Revision = 1 });

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => store.Db.SaveChangesAsync());

        await using var read = store.Fresh();

        Assert.Equal("theirs", (await read.Catalogs.SingleAsync()).Name);
    }

    [Fact]
    public async Task the_same_message_twice_writes_nothing_the_second_time()
    {
        await using var store = await MapperStore<ExtremeContext>.Create();

        store.Db.Add(new Tag { Id = 1, Name = "t" });
        await store.Db.SaveChangesAsync();

        var message = new CatalogDto
        {
            Id = 1,
            Name = "c",
            Tag = new TagDto { Id = 1 },
            Sections = [new SectionDto { Id = 1, Name = "s", Items = [new SectionItemDto { Id = 1, Name = "i" }] }],
        };

        await store.Mapper.Upsert<CatalogDto, Catalog>(message);
        await store.Db.SaveChangesAsync();

        await using var second = store.Fresh();

        await MapperStore<ExtremeContext>.MapperOver(second).Map<CatalogDto, Catalog>(message);

        second.ChangeTracker.DetectChanges();

        Assert.DoesNotContain(second.ChangeTracker.Entries(), e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted);
    }

    [Fact]
    public async Task a_cancelled_run_stops_and_saves_nothing()
    {
        await using var store = await MapperStore<ExtremeContext>.Create();

        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.Mapper.Upsert<CatalogDto, Catalog>(new CatalogDto { Id = 1, Sections = [new SectionDto { Id = 1 }] }, cancelled.Token));

        await using var read = store.Fresh();

        Assert.Equal(0, await read.Catalogs.CountAsync());
    }

    [Fact]
    public async Task five_thousand_children_round_trip()
    {
        await using var store = await MapperStore<ExtremeContext>.Create();

        await store.Mapper.Upsert<CatalogDto, Catalog>(new CatalogDto { Id = 1, Sections = Enumerable.Range(1, 5000).Select(i => new SectionDto { Id = i, Name = "s" + i }).ToList() });
        await store.Db.SaveChangesAsync();
        store.Db.ChangeTracker.Clear();

        // Odd ids stay and are renamed, even ids go, two thousand five hundred arrive.
        await store.Mapper.Map<CatalogDto, Catalog>(new CatalogDto
        {
            Id = 1,
            Sections = Enumerable.Range(1, 5000).Where(i => i % 2 == 1).Concat(Enumerable.Range(5001, 2500)).Select(i => new SectionDto { Id = i, Name = "r" + i }).ToList(),
        });
        await store.Db.SaveChangesAsync();

        await using var read = store.Fresh();

        var sections = await read.Set<Section>().ToListAsync();

        Assert.Equal(5000, sections.Count);
        Assert.DoesNotContain(sections, s => s.Id <= 5000 && s.Id % 2 == 0);
        Assert.All(sections, s => Assert.Equal("r" + s.Id, s.Name));
    }

    [Fact]
    public async Task a_deep_projection_runs_as_one_query()
    {
        await using var store = await MapperStore<ExtremeContext>.Create();

        store.Db.Add(new Tag { Id = 1, Name = "t" });
        store.Db.Add(new Catalog
        {
            Id = 1,
            Name = "c",
            Kind = CatalogKind.Seasonal,
            TagId = 1,
            Sections =
            [
                new Section { Id = 1, Name = "s1", Items = [new SectionItem { Id = 1, Name = "i1", TagId = 1 }, new SectionItem { Id = 2, Name = "i2" }] },
                new Section { Id = 2, Name = "s2" },
            ],
        });
        await store.Db.SaveChangesAsync();

        await using var read = store.Fresh();

        var row = await MapperStore<ExtremeContext>.MapperOver(read).Project<Catalog, CatalogRow>(read.Catalogs).TagWith("deep").SingleAsync();

        Assert.Equal("Seasonal", row.Kind);
        Assert.Equal(1, row.TagId);
        Assert.Equal(["s1", "s2"], row.Sections!.Select(s => s.Name).Order());

        var s1 = row.Sections!.Single(s => s.Name == "s1");

        Assert.Equal(["i1", "i2"], s1.Items!.Select(i => i.Name).Order());
        Assert.Equal("t", s1.Items!.Single(i => i.Name == "i1").Tag!.Name);
        Assert.Null(s1.Items!.Single(i => i.Name == "i2").Tag);
        Assert.Equal(0, s1.Items!.Single(i => i.Name == "i2").TagId);
        Assert.Empty(row.Sections!.Single(s => s.Name == "s2").Items!);
    }

    static NodeDto Tree(int id, string name, params NodeDto[] children)
    {
        return new NodeDto { Id = id, Name = name, Children = [.. children] };
    }

    public enum CatalogKind
    {
        Regular,
        Seasonal,
    }

    public class CatalogDto
    {
        public int Id { get; set; }

        public string? Name { get; set; }

        public int Revision { get; set; }

        public TagDto? Tag { get; set; }

        public List<SectionDto>? Sections { get; set; }
    }

    public class SectionDto
    {
        public int Id { get; set; }

        public string? Name { get; set; }

        public TagDto? Tag { get; set; }

        public int? TagId { get; set; }

        public List<SectionItemDto>? Items { get; set; }
    }

    public class SectionItemDto
    {
        public int Id { get; set; }

        public string? Name { get; set; }

        public TagDto? Tag { get; set; }
    }

    public class TagDto
    {
        public int Id { get; set; }

        public string? Name { get; set; }
    }

    public class CatalogRow
    {
        public string? Name { get; set; }

        public string? Kind { get; set; }

        public int TagId { get; set; }

        public List<SectionRow>? Sections { get; set; }
    }

    public class SectionRow
    {
        public string? Name { get; set; }

        public List<SectionItemRow>? Items { get; set; }
    }

    public class SectionItemRow
    {
        public string? Name { get; set; }

        public int TagId { get; set; }

        public TagDto? Tag { get; set; }
    }

    [MapFrom(typeof(CatalogDto))]
    [MapTo(typeof(CatalogRow))]
    public class Catalog
    {
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int Id { get; set; }

        public string? Name { get; set; }

        public CatalogKind Kind { get; set; }

        [ConcurrencyCheck]
        public int Revision { get; set; }

        public int? TagId { get; set; }

        public Tag? Tag { get; set; }

        [Composition]
        public List<Section>? Sections { get; set; }
    }

    public class Section
    {
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int Id { get; set; }

        public string? Name { get; set; }

        public int? TagId { get; set; }

        public Tag? Tag { get; set; }

        [Composition]
        public List<SectionItem>? Items { get; set; }
    }

    public class SectionItem
    {
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int Id { get; set; }

        public string? Name { get; set; }

        public int? TagId { get; set; }

        public Tag? Tag { get; set; }
    }

    [MapTo(typeof(TagDto))]
    public class Tag
    {
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int Id { get; set; }

        public string? Name { get; set; }
    }

    public class NodeDto
    {
        public int Id { get; set; }

        public string? Name { get; set; }

        public List<NodeDto>? Children { get; set; }
    }

    [MapFrom(typeof(NodeDto))]
    public class Node
    {
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int Id { get; set; }

        public string? Name { get; set; }

        public int? ParentId { get; set; }

        [Composition]
        public List<Node>? Children { get; set; }
    }

    public class ShelfDto
    {
        public int Id { get; set; }

        public List<SlotDto>? Slots { get; set; }
    }

    public class SlotDto
    {
        public int Row { get; set; }

        public int Column { get; set; }

        public string? Label { get; set; }
    }

    [MapFrom(typeof(ShelfDto))]
    public class Shelf
    {
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int Id { get; set; }

        [Composition]
        public List<Slot>? Slots { get; set; }
    }

    public class Slot
    {
        [Key]
        public int Row { get; set; }

        [Key]
        public int Column { get; set; }

        public string? Label { get; set; }
    }

    public class ExtremeContext(DbContextOptions<ExtremeContext> options) : DbContext(options)
    {
        public DbSet<Catalog> Catalogs { get; set; } = null!;

        public DbSet<Node> Nodes { get; set; } = null!;

        public DbSet<Shelf> Shelves { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Node>().HasMany(n => n.Children).WithOne().HasForeignKey(n => n.ParentId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<Catalog>().HasMany(c => c.Sections).WithOne().OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<Section>().HasMany(s => s.Items).WithOne().OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<Slot>().Property<int>("ShelfId");
            modelBuilder.Entity<Slot>().HasKey("ShelfId", nameof(Slot.Row), nameof(Slot.Column));
        }
    }
}
