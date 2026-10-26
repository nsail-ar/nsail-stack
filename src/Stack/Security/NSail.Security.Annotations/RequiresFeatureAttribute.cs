// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Security.Annotations;

/// <summary>Declares the feature a message belongs to: a send the install's record about this
/// tenant says the feature is off for is refused by the gate, and a page or an entry gated on
/// that message is not offered. One key per message, compared and never parsed — the Stack
/// names no feature and knows none, exactly as it carries a plan's word without naming a plan.
///
/// <para>A key nobody has written about a tenant reads as ON: whoever sells the features
/// declares what each one defaults to, and the permissive answer is the only one that leaves an
/// install nobody has written a bag for behaving as it did before the bag existed.</para></summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class RequiresFeatureAttribute : Attribute
{
    public RequiresFeatureAttribute(string feature)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(feature);

        Feature = feature;
    }

    public string Feature { get; }
}
