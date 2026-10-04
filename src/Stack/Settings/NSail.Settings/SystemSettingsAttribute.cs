// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.Settings;

/// <summary>Marks a settings POCO as system-wide: a single row, readable without a user
/// in context (background jobs, notifications). Without it, settings are per-user.</summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class SystemSettingsAttribute : Attribute;
