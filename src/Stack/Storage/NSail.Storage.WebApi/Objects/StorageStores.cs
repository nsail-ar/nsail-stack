// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Injection.Annotations;
using NSail.Problems;

namespace NSail.Storage.Objects;

// The store names this host declared, the default first, and the install's target for each.
[Injectable]
public sealed class StorageStores
{
    readonly IReadOnlyList<StorageDeclaration> _declarations;

    public StorageStores(IEnumerable<StorageDeclaration> declarations)
    {
        _declarations = declarations.ToList();
    }

    public IReadOnlyList<string> Names
    {
        get
        {
            return _declarations
                .Select(declaration => declaration.Name)
                .Prepend(StorageNames.Default)
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }
    }

    // The last declaration that named a target wins, so the host's configuration overrides
    // whatever a kit declared before it.
    public StorageOptions? Install(string name)
    {
        return _declarations.LastOrDefault(declaration => declaration.Name == name && declaration.Install is not null)?.Install;
    }

    // A name from the wire must be one the host declared: the settings row keeps a slot per
    // name, and an undeclared one would be a slot no consumer ever reads.
    public string Require(string name)
    {
        if (!Names.Contains(name, StringComparer.Ordinal))
        {
            throw new BusinessException(BusinessProblem.RuleViolation(
                "StorageNotDeclared",
                $"No store named '{name}' is declared in this host.",
                new Dictionary<string, string> { ["name"] = name }));
        }

        return name;
    }
}
