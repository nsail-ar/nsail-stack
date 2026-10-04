// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using System.ComponentModel.DataAnnotations;
using NSail.Messaging;

namespace NSail.Sample.Strings;

// The catalog a browser that runs no .NET reads its texts from: the host's merged strings for one
// language, kits and app alike, so a key resolves to the same sentence on both sides.
[Http(Get, "api/sample/strings/{language}")]
public class GetStrings : IMessage<Dictionary<string, string>>
{
    [AsRoute]
    [Required]
    [MaxLength(10)]
    public required string Language { get; set; }
}
