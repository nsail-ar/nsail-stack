// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Problems;

namespace NSail.Messaging.WebApi;

/// <summary>Writes a Problem as a document, for a caller that asked for one. Declared here and
/// implemented by the layer that composes the app's UI, so the error handler renders a page
/// without knowing what draws it: a host that composes no app registers nothing, the resolve
/// answers null and the response stays the machine's.</summary>
public interface IProblemDocumentProvider
{
    Task Write(HttpContext context, Problem problem);
}
