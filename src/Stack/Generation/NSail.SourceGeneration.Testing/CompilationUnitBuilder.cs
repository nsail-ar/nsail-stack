// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Diagnostics.CodeAnalysis;

namespace NSail.SourceGeneration.Testing
{
    /// <summary>
    /// Helps build Roslyn compilations for different .NET frameworks from source files or text.
    /// </summary>
    public class CompilationUnitBuilder
    {
        private readonly List<(string fileName, string code)> _sources = new();
        private readonly List<MetadataReference> _references = new();

        /// <summary>
        /// Adds a C# source file by name and text.
        /// </summary>
        public CompilationUnitBuilder AddSource(string fileName, string code)
        {
            _sources.Add((fileName, code));
            return this;
        }

        /// <summary>
        /// Adds a C# source file by name and text.
        /// </summary>
        [SuppressMessage("MicrosoftCodeAnalysisCorrectness", "RS1035:Do not use APIs banned for analyzers", Justification = "To be used only in test projects")]
        public CompilationUnitBuilder AddSourceFile(string filePath)
        {
            AddSource(Path.GetFileName(filePath), File.ReadAllText(filePath));
            return this;
        }

        [SuppressMessage("MicrosoftCodeAnalysisCorrectness", "RS1035:Do not use APIs banned for analyzers", Justification = "To be used only in test projects")]
        public CompilationUnitBuilder AddSourceFiles(string glob)
        {
            var normalized = glob
                .Replace('\\', Path.DirectorySeparatorChar)
                .Replace('/', Path.DirectorySeparatorChar);

            var recursive = normalized.Contains("**", StringComparison.Ordinal);

            string baseDir;
            string searchPattern;

            if (recursive)
            {
                var idx = normalized.IndexOf("**", StringComparison.Ordinal);

                if (idx > 0)
                {
                    var separatorIndex = normalized.LastIndexOf(
                        Path.DirectorySeparatorChar,
                        idx - 1,
                        idx);

                    if (separatorIndex >= 0)
                    {
                        baseDir = normalized.Substring(0, separatorIndex);
                    }
                    else
                    {
                        baseDir = ".";
                    }
                }
                else
                {
                    baseDir = ".";
                }

                searchPattern = Path.GetFileName(normalized);
            }
            else
            {
                baseDir = Path.GetDirectoryName(normalized) ?? ".";
                searchPattern = Path.GetFileName(normalized);
            }

            if (string.IsNullOrWhiteSpace(searchPattern))
            {
                searchPattern = "*.cs";
            }

            var searchOption = recursive
                ? SearchOption.AllDirectories
                : SearchOption.TopDirectoryOnly;

            foreach (var filePath in Directory.EnumerateFiles(baseDir, searchPattern, searchOption))
            {
                AddSource(Path.GetFileName(filePath), File.ReadAllText(filePath));
            }

            return this;
        }

        /// <summary>
        /// Adds a metadata reference (e.g., an assembly).
        /// </summary>
        public CompilationUnitBuilder AddReference(MetadataReference reference)
        {
            _references.Add(reference);
            return this;
        }

        /// <summary>
        /// Adds the containing assembly of the specified type as a metadata reference.
        /// </summary>
        public CompilationUnitBuilder AddContainingAssembly<TType>()
        {
            var assembly = typeof(TType).Assembly;
            if (!string.IsNullOrEmpty(assembly.Location))
            {
                _references.Add(MetadataReference.CreateFromFile(assembly.Location));
            }
            return this;
        }

        /// <summary>
        /// Adds references for a given .NET framework (e.g., netstandard2.0, net8.0).
        /// </summary>
        public CompilationUnitBuilder AddFrameworkReferences(string targetFramework)
        {
            // Use Microsoft.NETCore.App.Ref or Microsoft.NETStandard.Library.Ref for real projects.
            // For test purposes, use the current runtime assemblies.
            var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
                .Select(a => MetadataReference.CreateFromFile(a.Location));

            _references.AddRange(assemblies);
            return this;
        }

        /// <summary>
        /// Builds a CSharpCompilation for the specified target framework.
        /// </summary>
        public CSharpCompilation Build(string assemblyName)
        {
            var parseOptions = new CSharpParseOptions(LanguageVersion.Latest);
            var syntaxTrees = _sources.Select(src => CSharpSyntaxTree.ParseText(src.code, parseOptions, src.fileName));

            var compilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary);

            return CSharpCompilation.Create(
                assemblyName,
                syntaxTrees,
                _references,
                compilationOptions
            );
        }
    }
}