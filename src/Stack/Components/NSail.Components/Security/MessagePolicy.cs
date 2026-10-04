// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using NSail.Security;

namespace NSail.Components;

/// <summary>The requirement a message-typed [Authorize&lt;TMessage&gt;] turns into: the page
/// is authorized when the session could ever send this message type (PreAuthorize).</summary>
public sealed class MessageRequirement : IAuthorizationRequirement
{
    public MessageRequirement(Type messageType)
    {
        MessageType = messageType;
    }

    public Type MessageType { get; }
}

/// <summary>Answers the requirement optimistically against SecurityManager — the same
/// PreAuthorize gate the menu and action panels use, so a page, its menu entry and its
/// row action agree on visibility. The server answers from stored and built-in policies,
/// the client from the effective set it fetched, through this same code.</summary>
public sealed class MessageAuthorizationHandler : AuthorizationHandler<MessageRequirement>
{
    readonly SecurityManager _security;
    readonly SessionProvider _sessions;

    public MessageAuthorizationHandler(SecurityManager security, SessionProvider sessions)
    {
        _security = security;
        _sessions = sessions;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, MessageRequirement requirement)
    {
        await _security.EnsureLoaded().ConfigureAwait(false);

        if (_security.PreAuthorize(requirement.MessageType, _sessions.Session))
        {
            context.Succeed(requirement);
        }
    }
}

/// <summary>Fabricates a policy for a "msg:{AssemblyQualifiedName}" name (produced by
/// AuthorizeAttribute&lt;T&gt;) and delegates every other name — plus the default and fallback
/// policies — to the built-in provider, so plain [Authorize]/[AllowAnonymous] and the
/// deny-by-default fallback keep working untouched. Both hosts register it: without it the
/// client's AuthorizeRouteView asks for a policy name nobody knows and the page dies.</summary>
public sealed class MessagePolicyProvider : IAuthorizationPolicyProvider
{
    // The type argument is irrelevant — the prefix is a compile-time const on the open
    // attribute, and reading it here is what keeps writer and reader of the name together.
    const string MessagePrefix = AuthorizeAttribute<object>.MessagePolicyPrefix;

    readonly DefaultAuthorizationPolicyProvider _default;

    public MessagePolicyProvider(IOptions<AuthorizationOptions> options)
    {
        _default = new DefaultAuthorizationPolicyProvider(options);
    }

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith(MessagePrefix, StringComparison.Ordinal))
        {
            var typeName = policyName[MessagePrefix.Length..];
            var messageType = Type.GetType(typeName)
                ?? throw new InvalidOperationException($"Authorize policy references an unknown message type '{typeName}'.");

            var policy = new AuthorizationPolicyBuilder()
                .AddRequirements(new MessageRequirement(messageType))
                .Build();

            return Task.FromResult<AuthorizationPolicy?>(policy);
        }

        return _default.GetPolicyAsync(policyName);
    }

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync()
    {
        return _default.GetDefaultPolicyAsync();
    }

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync()
    {
        return _default.GetFallbackPolicyAsync();
    }
}
