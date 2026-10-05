// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.TypeScriptGenerator;

// NSail.TypeScriptGenerator @<file>, one key=value per line: assembly, out, source, reference,
// define — what NSail.TypeScriptGenerator.targets hands over from the Sdk's own compile.
if (args.Length != 1 || !args[0].StartsWith('@'))
{
    Console.Error.WriteLine("usage: NSail.TypeScriptGenerator @<response file>");

    return 2;
}

try
{
    var arguments = SdkArguments.Read(args[0][1..]);
    var compilation = SdkCompilation.Create(arguments);
    var model = SdkReader.Read(compilation);
    var text = TypeScriptWriter.Write(model);

    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(arguments.Output))!);

    // Unchanged text is not rewritten, so a dev server watching the file does not reload on a
    // build that changed nothing in the contract.
    if (!File.Exists(arguments.Output) || File.ReadAllText(arguments.Output) != text)
    {
        File.WriteAllText(arguments.Output, text);
    }

    Console.WriteLine($"NSail.TypeScriptGenerator: {model.Messages.Count} messages, {model.Models.Count} models, {model.Enums.Count} enums -> {arguments.Output}");

    return 0;
}
catch (SdkReadException exception)
{
    Console.Error.WriteLine($"error NSTS001: {exception.Message}");

    return 1;
}
