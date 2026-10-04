// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components;

/// <summary>How many things are waiting behind a menu entry, drawn beside its label so a
/// person sees there is work there without opening it. Name is the entry's Name — the same
/// merge key and the same localization key — so a contributor never has to know where the app
/// arranged the door it counts for, and a count whose entry the app hid is drawn nowhere.
///
/// <para>Separate from <see cref="INavMenuContributor"/> because the two answer at different
/// rates: the tree is a function of the session and is asked once for it, while a number is the
/// point of being a number and is asked again as the person moves through the app — and again
/// whenever anything publishes <see cref="NavMenuCountsChanged"/>, which is how work that
/// arrives while nobody is moving reaches the drawer. A count that rode the tree would be as
/// stale as the tree, which is to say stale for the whole session.</para>
///
/// <para>So a contributor is asked often and at moments it did not choose: the implementation is
/// a read, never a computation worth remembering, and it owes nothing to whoever asked.</para></summary>
public interface INavMenuCount
{
    string Name { get; }

    /// <summary>Zero for nothing to show, and zero is also the honest answer when the count
    /// cannot be had: a drawer is chrome, so a number that failed to arrive is drawn as no
    /// number rather than as an error over the app's own map.</summary>
    Task<int> GetCount();
}
