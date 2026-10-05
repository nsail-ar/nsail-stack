// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace NSail.TypeScriptGenerator;

// The Sdk's own compile, rebuilt from the sources and references MSBuild handed it, so the
// symbols carry what the C# generators see: nullable annotations, `required`, attribute
// arguments, and the declaring syntax HttpModelFactory reads.
static class SdkCompilation
{
    public static CSharpCompilation Create(SdkArguments arguments)
    {
        var parse = new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: arguments.Defines);

        var trees = arguments.Sources
            .Where(File.Exists)
            .Select(path => CSharpSyntaxTree.ParseText(File.ReadAllText(path), parse, path));

        var references = arguments.References
            .Where(File.Exists)
            .Select(path => MetadataReference.CreateFromFile(path));

        return CSharpCompilation.Create(
            arguments.AssemblyName,
            trees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
    }
}
