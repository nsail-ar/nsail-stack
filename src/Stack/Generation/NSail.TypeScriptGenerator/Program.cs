// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.TypeScriptGenerator;

// NSail.TypeScriptGenerator --assembly <Sdk.dll> [--assembly <Sdk.dll> ...] --out <file.ts>
var assemblies = new List<string>();
string? output = null;

for (var i = 0; i < args.Length - 1; i++)
{
    switch (args[i])
    {
        case "--assembly":
            assemblies.Add(args[++i]);
            break;
        case "--out":
            output = args[++i];
            break;
    }
}

if (assemblies.Count == 0 || output is null)
{
    Console.Error.WriteLine("usage: NSail.TypeScriptGenerator --assembly <Sdk.dll> [--assembly <Sdk.dll> ...] --out <file.ts>");

    return 2;
}

try
{
    var loaded = assemblies.Select(SdkLoader.Load).ToList();
    var model = SdkReader.Read(loaded);
    var text = TypeScriptWriter.Write(model);

    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);

    // Unchanged text is not rewritten, so a dev server watching the file does not reload on a
    // build that changed nothing in the contract.
    if (!File.Exists(output) || File.ReadAllText(output) != text)
    {
        File.WriteAllText(output, text);
    }

    Console.WriteLine($"NSail.TypeScriptGenerator: {model.Messages.Count} messages, {model.Models.Count} models, {model.Enums.Count} enums -> {output}");

    return 0;
}
catch (SdkReadException exception)
{
    Console.Error.WriteLine($"error NSTS001: {exception.Message}");

    return 1;
}
