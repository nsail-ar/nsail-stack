// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using NSail.Problems;

namespace NSail.Components;

public abstract class AsyncEventArgs
{
    public CancellationToken CancellationToken { get; internal set; }

    /// <summary>The refusal the handler is answering with — the one channel a screen has for a
    /// "no" it decided ITSELF, before or instead of any round trip. The <see cref="Runner"/>
    /// that raised the handler reports whatever is left here, so it reaches the component's own
    /// placement (a form draws it under the field it names or in its problem line) by the same
    /// path a refusal thrown by a send takes. The text is the screen's own, already localized:
    /// nothing downstream invents wording. Leaving it null is success.</summary>
    public Problem? Problem { get; set; }
}
