// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;

namespace NSail.TypeScriptGenerator;

// The Sdk is loaded into the default context on purpose: the annotations and the metadata
// provider this tool compiles against must be the very types the Sdk's attributes and
// namespaces were built with, or every typed attribute read would miss.
static class SdkLoader
{
    static readonly List<string> _probes = [];

    static SdkLoader()
    {
        AssemblyLoadContext.Default.Resolving += Resolve;

        // An Sdk's own bin carries its project references but not the framework packages they
        // lean on (Microsoft.Extensions.*), which the ASP.NET shared framework does.
        var runtime = RuntimeEnvironment.GetRuntimeDirectory().TrimEnd(Path.DirectorySeparatorChar);
        var version = Path.GetFileName(runtime);
        var shared = Path.GetDirectoryName(Path.GetDirectoryName(runtime))!;
        var aspNet = Path.Combine(shared, "Microsoft.AspNetCore.App", version);

        if (Directory.Exists(aspNet))
        {
            _probes.Add(aspNet);
        }
    }

    public static Assembly Load(string path)
    {
        var full = Path.GetFullPath(path);

        _probes.Insert(0, Path.GetDirectoryName(full)!);

        return AssemblyLoadContext.Default.LoadFromAssemblyPath(full);
    }

    static Assembly? Resolve(AssemblyLoadContext context, AssemblyName name)
    {
        foreach (var probe in _probes)
        {
            var file = Path.Combine(probe, name.Name + ".dll");

            if (File.Exists(file))
            {
                return context.LoadFromAssemblyPath(file);
            }
        }

        return null;
    }
}
