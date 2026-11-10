// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Harness.Api;

// A job that ended, as a report to Ops carries it: never its log, nor where its log lives. Its
// machine, Id and Started name it; the Id alone repeats every year.
public sealed record EndedJob(string Id, string Work, int? Story, DateTimeOffset Started, DateTimeOffset Ended, string? Outcome, string? Result);
