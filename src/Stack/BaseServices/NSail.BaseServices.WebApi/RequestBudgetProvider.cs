// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.BaseServices.WebApi;

/// <summary>How many requests this scope's tenant may send per minute, for
/// <see cref="RequestBudgetMiddleware"/> to spend. The base answers null — no budget — which is
/// what every install is until whoever sells the plan says otherwise: the Stack names no
/// entitlement key, so the figure is the product's to read (typically
/// <c>FeatureProvider.Limit</c> on a key it declares) and this is the seam it reads it
/// through.</summary>
public class RequestBudgetProvider
{
    public virtual Task<int?> PerMinute(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<int?>(null);
    }
}
