// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.Security;

/// <summary>Answers "which organization is this anonymous request for" — the pre-auth
/// counterpart to Session.OrganizationId, needed by endpoints that must resolve an
/// organization before anyone has signed in (the branding logo splash, the sign-in
/// page's brand). The default answers none (no organization, no product without Iam
/// breaks); Iam registers the real implementation — the organization whose own label is
/// exactly the one the request's host carries under the tenant's base domain, else the
/// single root organization, else ambiguous —
/// same optimistic-default shape as RelationProvider. A host chooses which branch brands
/// an anonymous page; it is never a tenant selector and never an authorization input, and
/// once a session exists the session's organization wins.</summary>
public class RequestOrganizationProvider
{
    public virtual Task<RequestOrganization> GetRequestOrganization(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(RequestOrganization.None);
    }
}
