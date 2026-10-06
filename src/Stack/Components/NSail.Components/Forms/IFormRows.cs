// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components;

// The hosts holding a row open inside a document, so its submit can settle them before it
// validates. An open row's values are already the document's — NsListEditor.Add appends to Items
// before any Confirmar — so the save was going to write that row either way, and the row's own
// rule is what has to answer for it: a field re-posts its own problem in the very pass that
// lifted it (NsFieldBase) and a page's OnCommit has nobody to re-raise it, so without this seam
// the submit lifts the refusal the row was holding and sends the bad row to the server
// (intentional-ui.md, Refusal placement).
interface IFormRows
{
    void Claim(IFormRow row);

    void Release(IFormRow row);
}

// A host editing a row of its own inside someone else's document.
interface IFormRow
{
    // Closes the row open here by asking its rule again on the values on screen, and answers
    // whether the document may go on: true for a row nothing refuses — the editor closes and the
    // document saves in one tap — and for a host with no row open at all. False means the row
    // refused, and the host owes the person that answer where they are looking: the submit returns
    // on a false having drawn nothing of its own, so a host that answers false mutely is a Guardar
    // that does nothing (nsail#1029). The one mute false the doctrine allows is the Abort shape,
    // a "no" the person just gave (intentional-ui.md, Refusal placement) — the constraint is
    // written where a host reads it, on RowEditEventArgs.Cancel.
    Task<bool> Settle();
}
