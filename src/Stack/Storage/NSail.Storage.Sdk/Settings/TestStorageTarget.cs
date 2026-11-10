// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Messaging;
using System.ComponentModel.DataAnnotations;

namespace NSail.Storage.Settings;

// Carries only the store's name on purpose: it exercises the target that name has SAVED, which
// is the one its consumer will write to. A message carrying the form's values would prove a
// configuration that exists only in one browser.
//
// It answers nothing either. A probe that worked has no fact to report beyond having worked, and
// a probe that failed is a refusal — so an answer with room for "it did not work" in it is a
// second way to say no, next to the one the screen already draws.
[Http(Post, "api/storage/{name}/test")]
public class TestStorageTarget : IMessage
{
    [AsRoute]
    [MaxLength(100)]
    public required string Name { get; set; }
}
