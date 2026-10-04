// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using NSail.Security;

namespace NSail.Sample;

public static class Policies
{
    // No audience, so anonymous: the sample composes no Iam and has no sign-in, so its openness
    // is a policy the gate reads rather than a gate that was never turned on — a message this
    // grant does not name is refused like on any other host. Both roots register it: the server
    // enforces it, the client draws by it.
    public static void AddSamplePolicies(this IServiceCollection services)
    {
        services.AddBuiltInPolicy(new Policy
        {
            Name = "Sample.Policies.Everyone",
            Messages = ["Sample.*"],
        });
    }
}
