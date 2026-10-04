// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Dates;

/// <summary>The current business day, taken from the host's local clock — never UTC. Every
/// server-side "today" that becomes a stored <c>DateOnly</c> (a delivery, a sale, a
/// membership window) reads it from here: UTC rolls over hours before local midnight, which
/// would move an evening's work into tomorrow while the day is still open.</summary>
public static class BusinessDate
{
    public static DateOnly Today => DateOnly.FromDateTime(DateTime.Now);
}
