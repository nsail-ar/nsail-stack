// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components;

/// <summary>How hard a typed password is to guess, as advice under the field. It is never a
/// gate: NSail has no password policy, so nothing refuses a Weak one.</summary>
public enum PasswordStrength
{
    Weak,
    Medium,
    Strong
}
