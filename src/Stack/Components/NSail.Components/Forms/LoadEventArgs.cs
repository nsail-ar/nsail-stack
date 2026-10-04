// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components;

/// <summary>The read half of a form's contract, the twin of <see cref="SubmitEventArgs{TModel}"/>:
/// the handler answers with the model the form will edit, and the form owns everything that
/// happens when it does not. A document that legitimately does not exist yet is answered with a
/// new model — there the blank IS the data, and it is not a failure.</summary>
public sealed class LoadEventArgs<TModel> : AsyncEventArgs where TModel : class
{
    public TModel? Model { get; set; }
}
