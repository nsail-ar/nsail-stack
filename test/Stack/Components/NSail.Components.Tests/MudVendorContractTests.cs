// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Globalization;
using System.Reflection;
using System.Resources;
using System.Text.Json;
using MudBlazor;

namespace NSail.Components.Tests;

/// <summary>What the Stack takes from MudBlazor without a compile-time check: the localizer keys
/// its components request (accessible names MudBlazor would otherwise show in English) and the
/// private field NsAutocomplete writes by reflection. Each requested key must exist in MudBlazor's
/// own resources and in both strings files; an upgrade that renames a key or the field fails here.</summary>
public sealed class MudVendorContractTests
{
    // Keys requested by the Mud components the Stack renders; add a key when a component starts asking for it.
    static readonly string[] RequestedKeys =
    [
        "MudDataGridPager_RowsPerPage", "MudDataGridPager_InfoFormat", "MudDataGridPager_AllItems",
        "MudTablePager_FirstPage", "MudTablePager_PreviousPage", "MudTablePager_NextPage", "MudTablePager_LastPage",
        "MudTable_Loading", "MudTreeView_ExpandItem", "MudTreeView_CollapseItem", "MudNavGroup_ToggleExpand",
        "MudInput_Clear", "MudInput_Increment", "MudInput_Decrement",
        "MudSnackbar_Close", "MudAlert_Close", "MudDialog_Close", "MudChip_Close",
        "MudBaseDatePicker_Open", "MudBaseDatePicker_PrevMonth", "MudBaseDatePicker_NextMonth",
        "MudBaseDatePicker_PrevYear", "MudBaseDatePicker_NextYear", "MudTimePicker_Open",
        "MudColorPicker_Close", "MudColorPicker_GridView",
        "MudColorPicker_HideSwatches", "MudColorPicker_HueSlider", "MudColorPicker_ModeSwitch",
        "MudColorPicker_Open", "MudColorPicker_PaletteView", "MudColorPicker_ShowSwatches",
        "MudColorPicker_SpectrumView",
        "MudTabs_ScrollLeft", "MudTabs_ScrollRight", "MudTabs_ScrollUp", "MudTabs_ScrollDown",
    ];

    static HashSet<string> Keys(string file)
    {
        var assembly = typeof(NsMudLocalizer).Assembly;
        var name = assembly.GetManifestResourceNames().Single(n => n.EndsWith("." + file, StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(name)!;
        using var document = JsonDocument.Parse(stream, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        return document.RootElement.EnumerateObject().Select(p => p.Name).ToHashSet();
    }

    [Fact]
    public void Every_requested_mud_key_exists_in_MudBlazor()
    {
        var resources = new ResourceManager("MudBlazor.Resources.LanguageResource", typeof(MudAutocomplete<>).Assembly);

        var unknown = RequestedKeys.Where(k => resources.GetString(k, CultureInfo.InvariantCulture) is null).ToList();

        Assert.True(unknown.Count == 0, $"MudBlazor defines no: {string.Join(", ", unknown)}");
    }

    [Theory]
    [InlineData("strings.json")]
    [InlineData("strings.es.json")]
    public void Every_requested_mud_key_has_an_entry(string file)
    {
        var keys = Keys(file);

        var missing = RequestedKeys.Where(k => !keys.Contains("Mud." + k)).ToList();

        Assert.True(missing.Count == 0, $"{file} lacks: {string.Join(", ", missing)}");
    }

    [Fact]
    public void The_two_files_carry_the_same_keys()
    {
        var english = Keys("strings.json");
        var spanish = Keys("strings.es.json");

        var diff = english.Except(spanish).Concat(spanish.Except(english)).ToList();

        Assert.True(diff.Count == 0, $"Only in one file: {string.Join(", ", diff)}");
    }

    [Fact]
    public void MudAutocomplete_still_has_the_field_NsAutocomplete_writes()
    {
        var field = typeof(MudAutocomplete<object>).GetField("_skipNextEnterKeyUp", BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(field);
        Assert.Equal(typeof(bool), field.FieldType);
    }
}
