// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components;

/// <summary>A series' colour as a theme palette slot: the call site never writes a hex, so
/// the segment reads on whichever scheme is lit. Info, Success, Warning, Error and Neutral are
/// scheme constants and always tell apart; Primary is the install's accent, so it can land on
/// one of them. Secondary and Tertiary are states of that accent and are not offered.</summary>
public enum NsChartColor
{
    Primary,
    Info,
    Success,
    Warning,
    Error,
    Neutral
}

/// <summary>One stacked series: <paramref name="Key"/> matches the keys of each bucket's values,
/// <paramref name="Name"/> is already translated.</summary>
public sealed record NsBarSeries(string Key, string Name, NsChartColor Color);

/// <summary>One bar: its axis label, the longer text its hover leads with (the label when null),
/// and a value per series key. A key the bucket does not carry is zero.</summary>
public sealed record NsBarBucket(string Label, IReadOnlyDictionary<string, double> Values, string? Title = null);

/// <summary>A vertical line at the bucket at <paramref name="Bucket"/> (its index), saying
/// <paramref name="Text"/> on hover.</summary>
public sealed record NsBarMarker(int Bucket, string Text);
