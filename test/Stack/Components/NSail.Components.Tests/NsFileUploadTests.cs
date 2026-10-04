// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Bunit;
using Microsoft.AspNetCore.Components;
using NSail.Components.Tests.Fixtures;

namespace NSail.Components.Tests;

/// <summary>The single-value field: ONE file, replaced when another is picked, drawn entirely
/// by the house. What each test pins is anatomy, because the anatomy IS the design — a field
/// line carries exactly ONE action icon like every other Mud field, and the clear only leaves
/// that line for the image when a preview created a second surface to put it on.</summary>
public sealed class NsFileUploadTests : UploadBunitContext
{
    /// <summary>No preview, empty: the line says there is nothing and offers the one action
    /// there is to take. No image, no clear.</summary>
    [Fact]
    public void WithoutPreview_Empty_OffersTheTriggerAlone()
    {
        var cut = Render<NsFileUpload>(ps => ps.Add(p => p.Accept, "image/*"));

        Assert.Contains("Ningún archivo elegido", cut.Markup);
        Assert.Equal("Subir", cut.Find(".ns-upload-line button").TextContent.Trim());
        Assert.Empty(cut.FindAll(".ns-upload-clear"));
        Assert.Empty(cut.FindAll("img"));
    }

    /// <summary>No preview, filled: the line shows the FILE NAME and the one action icon is
    /// the clear — Mud's own idiom. The trigger is gone, so replacing is clear-then-browse,
    /// which is exactly what Leonardo settled.</summary>
    [Fact]
    public void WithoutPreview_Filled_ShowsTheNameAndOnlyTheClear()
    {
        var cut = Render<UploadValueHost>(ps => ps.Add(p => p.Store, FileStore));

        Pick(cut, "receta.pdf");

        Assert.Single(cut.Instance.Raised);
        Assert.Contains("receta.pdf", cut.Markup);
        Assert.Single(cut.FindAll(".ns-upload-clear"));
        Assert.DoesNotContain("Subir", cut.Markup);
        Assert.Empty(cut.FindAll("img"));
    }

    /// <summary>WITH preview, filled: the image is the second surface, so the X lives ON it
    /// and the field line keeps the upload trigger — which is what makes replacing an in-place
    /// act. Two icons on one line would read wrong beside other fields; these are on two
    /// surfaces, which is the whole reason the preview earns the second one.</summary>
    [Fact]
    public void WithPreview_Filled_PutsTheClearOnTheImageAndKeepsTheTrigger()
    {
        var id = FileStore.Seed("logo.png");

        var cut = Render<NsFileUpload>(ps => ps
            .Add(p => p.Preview, true)
            .Add(p => p.Value, (Guid?)id));

        Assert.Single(cut.FindAll(".ns-upload-card img"));
        Assert.Single(cut.FindAll(".ns-upload-card .ns-upload-clear"));
        Assert.Empty(cut.FindAll(".ns-upload-line .ns-upload-clear"));
        Assert.Equal("Subir", cut.Find(".ns-upload-line button").TextContent.Trim());
    }

    /// <summary>The contract itself: one value. Picking over a file already held REPLACES it —
    /// the field raises the new id, never a second one beside the first, and one thumbnail
    /// with one clear is all that is ever on screen.</summary>
    [Fact]
    public void PickingOverAValue_ReplacesItInsteadOfAddingToIt()
    {
        var cut = Render<UploadValueHost>(ps => ps
            .Add(p => p.Store, FileStore)
            .Add(p => p.Preview, true));

        Pick(cut, "viejo.png");
        Pick(cut, "nuevo.png");

        Assert.Equal(2, cut.Instance.Raised.Count);
        Assert.NotEqual(cut.Instance.Raised[0], cut.Instance.Raised[1]);
        Assert.Equal(cut.Instance.Raised[1], cut.Instance.Value);
        Assert.Single(cut.FindAll(".ns-upload-card"));
        Assert.Single(cut.FindAll(".ns-upload-clear"));
        Assert.Contains("nuevo.png", cut.Markup);
        Assert.DoesNotContain("viejo.png", cut.Markup);
    }

    /// <summary>No vendor chrome leaks through: the picker's own file list — a purple chip
    /// carrying the filename and an X of its own — is suppressed, so the file is named once,
    /// by the house, on the field's line. Reported twice by Leonardo ("sigue mostrando ese
    /// pill feo"), which is why it is pinned by class name and not by eye.</summary>
    [Fact]
    public void AfterAPick_TheVendorsOwnFileChipIsNowhereInTheMarkup()
    {
        var cut = Render<UploadValueHost>(ps => ps
            .Add(p => p.Store, FileStore)
            .Add(p => p.Preview, true));

        Pick(cut, "logo2022.svg");

        Assert.DoesNotContain("mud-chip", cut.Markup);
        Assert.DoesNotContain("mud-file-upload-files-default-template", cut.Markup);
        Assert.Single(cut.FindAll(".ns-upload-clear"));
    }

    /// <summary>The empty preview shows the shape of what will land there: the house's own
    /// image glyph, drawn large inside the frame, so the box does not change size when the
    /// first file arrives.</summary>
    [Fact]
    public void WithPreview_Empty_DrawsTheImageGlyphInTheFrame()
    {
        var cut = Render<NsFileUpload>(ps => ps.Add(p => p.Preview, true));

        var path = cut.Find(".ns-upload-empty svg path");

        Assert.Contains(path.GetAttribute("d")!, NsIcons.Image.Markup);
    }

    /// <summary>A file over the limit is refused ON THE FIELD, with the limit in the sentence,
    /// and nothing reaches the store. The vendor's own MaxFileSize would have dropped it with
    /// nobody able to say so.</summary>
    [Fact]
    public void AFileOverTheLimit_IsRefusedOnTheFieldAndNeverStored()
    {
        var cut = Render<NsFileUpload>(ps => ps.Add(p => p.MaxSize, 4));

        Pick(cut, "grande.png", "0123456789");

        Assert.Contains("El archivo supera los 0 MB", cut.Markup);
        Assert.Empty(FileStore.Saved);
    }

    /// <summary>The API promise, and what this story narrowed: ONE value and nothing that
    /// hints at a second. Preview is the only shape parameter left; a list is a different
    /// component (NsMultiFileUpload), not a mode of this one. No Variant, no Color, no vendor
    /// knob; MaxSize and Store are the two the consumer genuinely owns.</summary>
    [Fact]
    public void NsFileUpload_ExposesOnlyTheSharedShape()
    {
        Assert.Equal(
            [
                "Accept", "AutoFocus", "Autocomplete", "Disabled", "For", "Grow", "Helper", "Immediate", "Label",
                "MaxSize", "Placeholder", "Preview", "ReadOnly", "Required", "Store", "Value",
                "ValueChanged", "ValueExpression"
            ],
            GetParameterNames(typeof(NsFileUpload)));
    }

    internal static string[] GetParameterNames(Type component)
    {
        return [.. component
            .GetProperties()
            .Where(p => p.GetCustomAttributes(typeof(ParameterAttribute), true).Length != 0)
            .Select(p => p.Name)
            .OrderBy(name => name, StringComparer.Ordinal)];
    }
}
