// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.Backup;

/// <summary>A backup or a restore that stopped on purpose. Carries no problem code: the
/// refusals it names are read by a person at a console, not by a client.</summary>
public sealed class BackupFailure : Exception
{
    public BackupFailure(string message) : base(message)
    {
    }
}
