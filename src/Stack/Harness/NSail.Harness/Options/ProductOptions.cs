// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Harness;

// A product a QA walk is walked on, by key (Harness:Products): the labels that name it, its test
// tenant, the names of the variables carrying the walker's sign-in, and how the harness knows which
// build the tenant runs (Live). Default: the product a story no label names is walked on.
public sealed class ProductOptions
{
    public string Name { get; set; } = "";
    public List<string> Labels { get; set; } = [];
    public bool Default { get; set; }
    public string Tenant { get; set; } = "";
    public string? UserVariable { get; set; }
    public string? PasswordVariable { get; set; }
    public LiveOptions Live { get; set; } = new();
}

// The build a tenant runs, as a tag of the repository: the newest tag starting with Tag, or — when
// Header names one — the tag `{Tag}{value}` of that response header on the tenant's Url (a tenant
// that cuts without deploying says its own build).
public sealed class LiveOptions
{
    public string Tag { get; set; } = "";
    public string? Header { get; set; }
    public string? Url { get; set; }
}
