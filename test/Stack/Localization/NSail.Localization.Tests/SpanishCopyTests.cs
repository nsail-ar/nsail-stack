// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Localization.Testing;

namespace NSail.Localization.Tests;

public sealed class SpanishCopyTests
{
    static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "NSail.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("NSail.sln not found above the test binaries.");
    }

    [Fact]
    public void NoSpanishStringCarriesSlang()
    {
        var src = Path.Combine(Root(), "src");

        Assert.NotEmpty(Directory.EnumerateFiles(src, "strings.es.json", SearchOption.AllDirectories));

        var offenders = SpanishCopy.Offenders(src);

        Assert.True(offenders.Count == 0, $"Slang in a Spanish catalog: {string.Join("; ", offenders)}");
    }

    [Theory]
    [InlineData("la plata", true)]
    [InlineData("platales", false)]
    [InlineData("Dale, seguí", true)]
    [InlineData("Validale", false)]
    [InlineData("Deshabilitá y chequealo", true)]
    [InlineData("estás chequeando", true)]
    [InlineData("laburás", true)]
    [InlineData("pibes", true)]
    [InlineData("mangos", true)]
    [InlineData("guitas", true)]
    [InlineData("barbara", true)]
    [InlineData("Bárbaros", true)]
    [InlineData("re lindas", true)]
    [InlineData("boludas", true)]
    [InlineData("quilombos", true)]
    [InlineData("un toque", true)]
    [InlineData("chequeé", true)]
    [InlineData("plataforma", false)]
    [InlineData("manguera", false)]
    [InlineData("guitarra", false)]
    [InlineData("un cheque, dos cheques", false)]
    [InlineData("toque la pantalla", false)]
    public void TheScanMatchesWholeWords(string text, bool slang)
    {
        Assert.Equal(slang, SpanishCopy.Find(text).Count > 0);
    }

    [Fact]
    public void TheScanNamesTheWordAsWritten()
    {
        Assert.Equal(["Chequealo", "bárbaras"], SpanishCopy.Find("Chequealo, son bárbaras 🎉 las ideas"));
    }

    [Fact]
    public void AProfileMayGlossSlangButACopyStringMayNot()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            File.WriteAllText(Path.Combine(dir.FullName, "strings.es.json"),
                """{ "App.Assistant.Profile": "Si dice plata o guita, es dinero.", "App.Page.Title": "Cobrá la plata" }""");

            var offenders = SpanishCopy.Offenders(dir.FullName);

            Assert.Equal(["strings.es.json: App.Page.Title: plata"], offenders);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }
}
