// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Messaging.Runtime.Validation;
using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace NSail.Messaging.Declarations.Tests;

// Covers the codes this repository mints. The products' sweep of the same name covers theirs.
public class CodedValidationStringTests
{
    private static readonly string[] Languages = ["strings.json", "strings.es.json"];

    [Fact]
    public void Every_declared_code_has_its_sentence_in_every_language()
    {
        var codes = DeclaredCodes();

        Assert.True(codes.Count >= 11, $"The sweep reads every code the Stack mints; it found {codes.Count}.");

        var missing = new List<string>();
        foreach (var language in Languages)
        {
            var catalog = Catalog(language);
            missing.AddRange(codes.Where(code => !catalog.ContainsKey($"Problems.{code}")).Select(code => $"{language}: Problems.{code}"));
        }

        Assert.True(missing.Count == 0, "A code has its sentence in each catalog: " + string.Join(", ", missing));
    }

    [Fact]
    public void The_problem_rows_of_the_two_catalogs_are_the_same_rows()
    {
        var english = Catalog("strings.json").Keys.Where(k => k.StartsWith("Problems.", StringComparison.Ordinal)).ToHashSet();
        var spanish = Catalog("strings.es.json").Keys.Where(k => k.StartsWith("Problems.", StringComparison.Ordinal)).ToHashSet();

        Assert.Empty(english.Except(spanish));
        Assert.Empty(spanish.Except(english));
    }

    private sealed class Uncoded : ValidationAttribute;

    private static HashSet<string> DeclaredCodes()
    {
        var codes = new HashSet<string>(StringComparer.Ordinal);

        var coded = typeof(ICodedValidation).Assembly.GetTypes()
            .Where(t => !t.IsAbstract && !t.IsInterface && typeof(ICodedValidation).IsAssignableFrom(t));
        foreach (var type in coded)
        {
            var attribute = (ICodedValidation)RuntimeHelpers.GetUninitializedObject(type);
            codes.Add(attribute.Code);
        }

        // The BCL vocabulary, minted by the same switch a server refusal and a screen both read.
        ValidationAttribute[] bcl =
        [
            new RequiredAttribute(), new CompareAttribute("Other"), new StringLengthAttribute(5), new MaxLengthAttribute(5),
            new RangeAttribute(1, 5), new EmailAddressAttribute(), new PhoneAttribute(), new UrlAttribute(),
            new RegularExpressionAttribute("x"), new Uncoded()
        ];
        foreach (var attribute in bcl)
        {
            codes.Add(MessageValidator.IssueFor(attribute, "Field").Code);
        }

        return codes;
    }

    private static Dictionary<string, string> Catalog(string file)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "NSail.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        var path = Path.Combine(directory.FullName, "src", "Stack", "Components", "NSail.Components.Mud", file);

        return JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path))!;
    }
}
