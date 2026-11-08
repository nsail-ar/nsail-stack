// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Security;

public sealed class MissingEncryptionKeyException : InvalidOperationException
{
    public MissingEncryptionKeyException(string message) : base(message)
    {
    }
}
