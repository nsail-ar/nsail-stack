// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Text.Json;

namespace NSail.Components.Tests.Fixtures;

/// <summary>The vendor's shipped stylesheet, as text, so a house rule that corrects a vendor rule
/// can pin the rule it corrects and a vendor upgrade cannot move it in silence.</summary>
internal static class VendorStylesheet
{
    internal static string Read()
    {
        // Through the build's own static-asset manifest, not beside the assembly: the test host
        // copies MudBlazor.dll into its output but none of the package's wwwroot, so the
        // stylesheet is only ever where the manifest's content roots say it is.
        var manifest = Path.Combine(
            AppContext.BaseDirectory,
            "NSail.Components.Mud.staticwebassets.runtime.json");

        Assert.True(File.Exists(manifest), $"The static asset manifest is missing: {manifest}");

        using var document = JsonDocument.Parse(File.ReadAllText(manifest));

        foreach (var root in document.RootElement.GetProperty("ContentRoots").EnumerateArray())
        {
            var css = Path.Combine(root.GetString()!, "MudBlazor.min.css");

            if (File.Exists(css))
            {
                return File.ReadAllText(css);
            }
        }

        Assert.Fail("MudBlazor.min.css was not found under any of the manifest's content roots.");

        return string.Empty;
    }
}
