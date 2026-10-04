// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using NSail.Localization;

namespace NSail.Localization.Tests;

// End-to-end through the public surface: AddStringsFromAssembly picks the calling
// assembly and reads its embedded strings.json + strings.{lang}.json overlay
// (see this project's root files).
public sealed class EmbeddedStringsTests
{
    private static StringManager Build(string culture)
    {
        CultureInfo.CurrentUICulture = new CultureInfo(culture);

        var services = new ServiceCollection();
        services.AddStringsFromAssembly();

        return services.BuildServiceProvider().GetRequiredService<StringManager>();
    }

    [Fact]
    public void Reads_embedded_strings_for_the_current_language()
    {
        var manager = Build("es");

        Assert.Equal("Hola", manager.Translate("Tests.Greeting"));
    }

    [Fact]
    public void Overlays_the_current_language_on_the_base_one()
    {
        var manager = Build("es");

        Assert.Equal("English only", manager.Translate("Tests.OnlyEnglish"));
    }

    [Fact]
    public void Falls_back_to_base_language_when_the_language_has_no_resource()
    {
        var manager = Build("de");

        Assert.Equal("Hello", manager.Translate("Tests.Greeting"));
    }

    [Fact]
    public void Lists_the_languages_it_has_a_resource_for()
    {
        var services = new ServiceCollection();
        services.AddStringsFromAssembly();

        var languages = services.BuildServiceProvider()
            .GetServices<IStringSource>()
            .SelectMany(source => source.Languages);

        Assert.Equal(["es"], languages);
    }
}
