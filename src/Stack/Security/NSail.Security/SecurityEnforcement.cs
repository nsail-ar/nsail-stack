// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.Security;

/// <summary>Registered only by <see cref="Setup.AddSecurityEnforcement"/>: it is how a
/// surface outside the Mediator pipeline tells an enforcing host from a permissive one
/// (a client, a Wasm host) without reading the interceptor registrations.</summary>
public sealed class SecurityEnforcement
{
}
