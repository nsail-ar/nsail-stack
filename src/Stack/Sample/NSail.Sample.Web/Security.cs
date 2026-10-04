// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using NSail.Security;

namespace NSail.Sample;

public static class Security
{
    public static void AddSampleSecurity(this IServiceCollection services)
    {
        services.AddSecurityEnforcement();
        services.AddSamplePermissions();
        services.AddSamplePolicies();
    }
}
