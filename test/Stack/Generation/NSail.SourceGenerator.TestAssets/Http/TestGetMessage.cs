// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using NSail.Messaging;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace NSail.SourceGenerator.TestAssets.Http;
 
[Http(Get, "api/v1/samples")]
public class TestGetMessage : IMessage<List<ExternalGetResult>>
{
    [Required]
    [NotNull]
    public bool Flag { get; set; } 
}

public class ExternalGetResult
{
    public int Code { get; set; }
}
