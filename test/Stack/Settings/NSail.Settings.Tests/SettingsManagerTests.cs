// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Settings;

namespace NSail.Settings.Tests;

public sealed class UserScopedSettings
{
    public string? Value { get; set; }
}

[SystemSettings]
public sealed class SystemScopedSettings
{
    public string? Value { get; set; }
}

public sealed class SettingsManagerTests
{
    [Fact]
    public void Storage_keys_come_from_the_metadata_provider()
    {
        Assert.Equal("Settings.UserScopedSettings", new NSail.Metadata.MetadataProvider().KeyFor(typeof(UserScopedSettings)));
    }

    [Fact]
    public void IsSystem_reads_the_marker_attribute()
    {
        Assert.False(SettingsManager.IsSystem(typeof(UserScopedSettings)));
        Assert.True(SettingsManager.IsSystem(typeof(SystemScopedSettings)));
    }
}
