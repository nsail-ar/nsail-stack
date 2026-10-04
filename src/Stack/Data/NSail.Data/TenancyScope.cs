// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.Data;

/// <summary>How far one switch of the tenancy seam reaches: to the install as a whole, or
/// to a tenant inside it.</summary>
public enum TenancyScope
{
    Install,
    Tenant,
}
