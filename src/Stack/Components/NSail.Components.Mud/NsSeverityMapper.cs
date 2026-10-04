// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using MudBlazor;

namespace NSail.Components;

internal static class NsSeverityMapper
{
    public static Severity ToMud(NsSeverity severity)
    {
        return severity switch
        {
            NsSeverity.Success => Severity.Success,
            NsSeverity.Warning => Severity.Warning,
            NsSeverity.Error => Severity.Error,
            _ => Severity.Info
        };
    }

    // The status channel's tone half: the class sets --ns-status-color and nothing else, so
    // the same four severities plus the neutral paint a word or a dot from one declaration.
    public static string ToStatusClass(NsSeverity? severity)
    {
        return severity switch
        {
            NsSeverity.Info => "ns-status-info",
            NsSeverity.Success => "ns-status-success",
            NsSeverity.Warning => "ns-status-warning",
            NsSeverity.Error => "ns-status-error",
            _ => "ns-status-muted"
        };
    }
}
