// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using NSail.Mapping;

namespace NSail.Mapping.Tests;

public sealed class EntityMapperTests
{
    static readonly Guid KindId = Guid.NewGuid();

    static (OwnerMapper mapper, FakeTracker tracker) Build()
    {
        var tracker = new FakeTracker();

        tracker.Seed(typeof(KindTarget), KindId, new KindTarget { Id = KindId, Name = "Billing" });

        return (new OwnerMapper(), tracker);
    }

    [Fact]
    public async Task Primitives_copy()
    {
        var (mapper, tracker) = Build();
        var target = new OwnerTarget();

        await mapper.Map(new OwnerSource { Id = Guid.NewGuid(), Name = "Acme" }, target, tracker, default);

        Assert.Equal("Acme", target.Name);
    }

    [Fact]
    public async Task An_association_moves_the_key_and_leaves_the_referenced_row_alone()
    {
        var (mapper, tracker) = Build();
        var kind = (KindTarget)(await tracker.Find(typeof(KindTarget), KindId, default))!;
        var target = new OwnerTarget();

        await mapper.Map(new OwnerSource { KindId = KindId }, target, tracker, default);

        Assert.Equal(KindId, target.KindId);
        Assert.Same(kind, target.Kind);

        // The whole point: pointing at the row must not be able to rewrite it, or create or
        // delete it.
        Assert.Equal("Billing", kind.Name);
        Assert.Empty(tracker.Added);
        Assert.Empty(tracker.Removed);
    }

    [Fact]
    public async Task An_association_to_nothing_is_null_not_a_new_row()
    {
        var (mapper, tracker) = Build();
        var target = new OwnerTarget();

        await mapper.Map(new OwnerSource { KindId = null }, target, tracker, default);

        Assert.Null(target.Kind);
        Assert.Empty(tracker.Added);
    }

    [Fact]
    public async Task A_composition_adds_what_the_source_brought()
    {
        var (mapper, tracker) = Build();
        var target = new OwnerTarget();
        var id = Guid.NewGuid();

        await mapper.Map(new OwnerSource { Items = [new ItemSource { Id = id, Name = "one" }] }, target, tracker, default);

        var item = Assert.Single(target.Items);
        Assert.Equal("one", item.Name);
        Assert.Same(item, Assert.Single(tracker.Added));
    }

    [Fact]
    public async Task A_composition_updates_a_match_in_place_instead_of_replacing_it()
    {
        var (mapper, tracker) = Build();
        var id = Guid.NewGuid();
        var existing = new ItemTarget { Id = id, Name = "before" };
        var target = new OwnerTarget { Items = [existing] };

        await mapper.Map(new OwnerSource { Items = [new ItemSource { Id = id, Name = "after" }] }, target, tracker, default);

        Assert.Same(existing, Assert.Single(target.Items));
        Assert.Equal("after", existing.Name);

        // Replacing instead of updating would orphan the row and lose everything the target
        // carries that the source never mentions.
        Assert.Empty(tracker.Added);
        Assert.Empty(tracker.Removed);
    }

    [Fact]
    public async Task A_composition_removes_what_the_source_dropped()
    {
        var (mapper, tracker) = Build();
        var kept = new ItemTarget { Id = Guid.NewGuid(), Name = "kept" };
        var gone = new ItemTarget { Id = Guid.NewGuid(), Name = "gone" };
        var target = new OwnerTarget { Items = [kept, gone] };

        await mapper.Map(new OwnerSource { Items = [new ItemSource { Id = kept.Id, Name = "kept" }] }, target, tracker, default);

        Assert.Same(kept, Assert.Single(target.Items));
        Assert.Same(gone, Assert.Single(tracker.Removed));
    }

    [Fact]
    public async Task A_composition_of_one_call_does_all_three()
    {
        var (mapper, tracker) = Build();
        var updated = new ItemTarget { Id = Guid.NewGuid(), Name = "before" };
        var removed = new ItemTarget { Id = Guid.NewGuid(), Name = "gone" };
        var target = new OwnerTarget { Items = [updated, removed] };
        var addedId = Guid.NewGuid();

        await mapper.Map(
            new OwnerSource
            {
                Items =
                [
                    new ItemSource { Id = updated.Id, Name = "after" },
                    new ItemSource { Id = addedId, Name = "new" },
                ],
            },
            target,
            tracker,
            default);

        Assert.Equal(2, target.Items.Count);
        Assert.Equal("after", updated.Name);
        Assert.Same(removed, Assert.Single(tracker.Removed));
        Assert.Equal(addedId, Assert.Single(tracker.Added) is ItemTarget added ? added.Id : Guid.Empty);
    }

    [Fact]
    public async Task A_collection_the_source_never_sent_is_left_alone()
    {
        var (mapper, tracker) = Build();
        var existing = new ItemTarget { Id = Guid.NewGuid(), Name = "kept" };
        var target = new OwnerTarget { Items = [existing] };

        // Null is "not sent", not "empty". A partial source must not read as an instruction
        // to delete every part it never mentions.
        await mapper.Map(new OwnerSource { Items = null }, target, tracker, default);

        Assert.Same(existing, Assert.Single(target.Items));
        Assert.Empty(tracker.Removed);
    }

    [Fact]
    public async Task An_empty_collection_does_clear_it()
    {
        var (mapper, tracker) = Build();
        var existing = new ItemTarget { Id = Guid.NewGuid() };
        var target = new OwnerTarget { Items = [existing] };

        await mapper.Map(new OwnerSource { Items = [] }, target, tracker, default);

        Assert.Empty(target.Items);
        Assert.Same(existing, Assert.Single(tracker.Removed));
    }
}
