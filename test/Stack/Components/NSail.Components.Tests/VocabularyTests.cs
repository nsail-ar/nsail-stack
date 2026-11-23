// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using NSail.Components.Tests.Fixtures;

namespace NSail.Components.Tests;

/// <summary>One name for each piece and no alias beside it: a renamed component or parameter is
/// gone under its old name, so a page that still writes it fails to compile instead of reading
/// two vocabularies. The stylesheet says <c>error</c> for the red tone, never <c>danger</c> —
/// <c>NsAs.Danger</c> stays, as the rank of a destructive act.</summary>
public sealed class VocabularyTests
{
    static readonly System.Reflection.Assembly Components = typeof(NsGrid).Assembly;

    [Theory]
    [InlineData("NsFormGrid", "NsGrid")]
    [InlineData("NsFormGridItem", "NsItem")]
    [InlineData("NsSendMessage", "NsComposer")]
    [InlineData("NsSince", "NsTimeAgo")]
    [InlineData("NsMissing`1", "NsNoOptions`1")]
    [InlineData("NsToolBar", "NsToolbar")]
    [InlineData("NsHint", "NsHelp")]
    public void ARenamedComponentResolvesOnlyUnderItsNewName(string old, string current)
    {
        Assert.Null(Components.GetType($"NSail.Components.{old}"));
        Assert.NotNull(Components.GetType($"NSail.Components.{current}"));
    }

    [Theory]
    [InlineData(typeof(NsActionToolbar), "Constant", "Door")]
    [InlineData(typeof(NsActionOutlet<,>), "Constant", "Door")]
    [InlineData(typeof(NsIcon), "Title", "Label")]
    [InlineData(typeof(NsStatusDot), "Title", "Label")]
    [InlineData(typeof(NsTimeBlock), "AriaLabel", "Label")]
    [InlineData(typeof(NsBarChart), "AriaLabel", "Label")]
    [InlineData(typeof(NsTabs), "ActivePanelIndex", "ActiveIndex")]
    [InlineData(typeof(NsTabs), "ActivePanelIndexChanged", "ActiveIndexChanged")]
    [InlineData(typeof(NsNumericField<decimal>), "Compact", "Stepper")]
    [InlineData(typeof(NsAlert), "As", "Severity")]
    public void ARenamedParameterIsAParameterOnlyUnderItsNewName(Type component, string old, string current)
    {
        Assert.False(IsParameter(component, old), $"{component.Name}.{old} is still a parameter.");
        Assert.True(IsParameter(component, current), $"{component.Name}.{current} is not a parameter.");
    }

    [Fact]
    public void TheStylesheetNamesNoDangerTone()
    {
        var selectors = Regex.Matches(HouseStylesheet.Read(), @"\.ns-[a-z-]*danger\b").Select(m => m.Value).ToArray();

        Assert.Empty(selectors);
    }

    static bool IsParameter(Type component, string name)
    {
        var property = component.GetProperty(name);

        return property?.GetCustomAttributes(typeof(ParameterAttribute), true).Length > 0;
    }
}
