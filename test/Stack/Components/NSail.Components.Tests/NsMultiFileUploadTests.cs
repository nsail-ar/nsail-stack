// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using MudBlazor;
using NSail.Components.Tests.Fixtures;

namespace NSail.Components.Tests;

/// <summary>The list field, founded because two screens genuinely hold a set of files — a
/// product's gallery and a clinical note's attachments. It carries the same house presentation
/// as the single field and the same absence of vendor chrome; what differs is that a set is
/// added to rather than replaced, and that its ORDER is part of its value.</summary>
public sealed class NsMultiFileUploadTests : UploadBunitContext
{
    /// <summary>No preview: a line per file, each with its own clear, and the trigger always
    /// visible — nothing has to be cleared before the next one is added.</summary>
    [Fact]
    public void WithoutPreview_ListsEachFileWithItsOwnClearAndAlwaysOffersTheTrigger()
    {
        var cut = Render<UploadValuesHost>(ps => ps.Add(p => p.Store, FileStore));

        Pick(cut, "estudio-1.pdf");
        Pick(cut, "estudio-2.pdf");

        Assert.Equal(2, cut.FindAll(".ns-upload-line .ns-upload-clear").Count);
        Assert.Contains("estudio-1.pdf", cut.Markup);
        Assert.Contains("estudio-2.pdf", cut.Markup);
        Assert.Equal("Subir", cut.Find(".ns-upload-actions button").TextContent.Trim());
    }

    /// <summary>WITH preview: a card per value, each carrying its own circled clear, and the
    /// trigger always visible.</summary>
    [Fact]
    public void WithPreview_RendersACardPerValueEachWithItsClear()
    {
        var model = new UploadValuesModel { Images = [FileStore.Seed("a.png"), FileStore.Seed("b.png")] };

        var cut = Render<UploadValuesHost>(ps => ps
            .Add(p => p.Store, FileStore)
            .Add(p => p.Preview, true)
            .Add(p => p.Model, model));

        Assert.Equal(2, cut.FindAll(".ns-upload-card").Count);
        Assert.Equal(2, cut.FindAll(".ns-upload-card .ns-upload-clear").Count);
        Assert.Equal("Subir", cut.Find(".ns-upload-actions button").TextContent.Trim());
    }

    /// <summary>The empty preview shows the same house glyph the single field shows: the shape
    /// of what will land there, so the box does not change size when the first file
    /// arrives.</summary>
    [Fact]
    public void WithPreview_Empty_DrawsTheImageGlyphInTheFrame()
    {
        var cut = Render<UploadValuesHost>(ps => ps
            .Add(p => p.Store, FileStore)
            .Add(p => p.Preview, true));

        var path = cut.Find(".ns-upload-empty svg path");

        Assert.Contains(path.GetAttribute("d")!, NsIcons.Image.Markup);
    }

    /// <summary>No vendor chrome here either: the picker's own chip list is suppressed, so
    /// each file is named exactly once, by the house.</summary>
    [Fact]
    public void AfterAPick_TheVendorsOwnFileChipIsNowhereInTheMarkup()
    {
        var cut = Render<UploadValuesHost>(ps => ps.Add(p => p.Store, FileStore));

        Pick(cut, "estudio.pdf");

        Assert.DoesNotContain("mud-chip", cut.Markup);
        Assert.DoesNotContain("mud-file-upload-files-default-template", cut.Markup);
        Assert.Single(cut.FindAll(".ns-upload-clear"));
    }

    /// <summary>The order IS the value: a drop raises the new order, and there is no cover flag
    /// anywhere — whatever sits first is the cover.</summary>
    [Fact]
    public async Task Reordering_RaisesTheNewOrderAndNothingElse()
    {
        var first = FileStore.Seed("a.png");
        var second = FileStore.Seed("b.png");
        var model = new UploadValuesModel { Images = [first, second] };

        var cut = Render<UploadValuesHost>(ps => ps
            .Add(p => p.Store, FileStore)
            .Add(p => p.Preview, true)
            .Add(p => p.Model, model));

        var container = cut.FindComponent<MudDropContainer<Guid>>();

        await cut.InvokeAsync(() => container.Instance.ItemDropped.InvokeAsync(
            new MudItemDropInfo<Guid>(second, "files", 0)));

        Assert.Equal([second, first], cut.Instance.Raised.Single());
        Assert.Equal([second, first], model.Images);
    }

    /// <summary>Clearing removes the id from the value and does nothing else — no store call,
    /// no server call. The deletion rides the consuming form's save, which is what lets an
    /// abandoned form lose nothing.</summary>
    [Fact]
    public async Task Clearing_RemovesTheIdFromTheValueAndTouchesNothingElse()
    {
        var first = FileStore.Seed("a.png");
        var second = FileStore.Seed("b.png");
        var model = new UploadValuesModel { Images = [first, second] };
        var savedBefore = FileStore.Saved.Count;

        var cut = Render<UploadValuesHost>(ps => ps
            .Add(p => p.Store, FileStore)
            .Add(p => p.Preview, true)
            .Add(p => p.Model, model));

        await cut.InvokeAsync(() => cut.FindAll(".ns-upload-card .ns-upload-clear")[0].Click());

        Assert.Equal([second], cut.Instance.Raised.Single());
        Assert.Equal(savedBefore, FileStore.Saved.Count);
    }

    /// <summary>Two components, one vocabulary: the list field takes exactly the parameters the
    /// single one takes — its Value is simply the ordered set. Nothing named Values survives,
    /// because a parameter that switches a component between two shapes is what this story
    /// removed.</summary>
    [Fact]
    public void NsMultiFileUpload_ExposesTheSameShapeAsTheSingleField()
    {
        Assert.Equal(
            NsFileUploadTests.GetParameterNames(typeof(NsFileUpload)),
            NsFileUploadTests.GetParameterNames(typeof(NsMultiFileUpload)));
    }
}
