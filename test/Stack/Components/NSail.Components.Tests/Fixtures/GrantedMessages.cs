// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.AspNetCore.Authorization;

namespace NSail.Components.Tests.Fixtures;

/// <summary>The session's grants, handed in literally: this is the seam
/// MessageAuthorizationHandler fills against a real SecurityManager, so a test doubles the
/// evaluator while the policy a page or a nav entry asks for is still fabricated by the
/// product's own MessagePolicyProvider.</summary>
public sealed class GrantedMessages(params Type[] granted) : AuthorizationHandler<MessageRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, MessageRequirement requirement)
    {
        if (granted.Contains(requirement.MessageType))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
