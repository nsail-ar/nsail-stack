// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Components.Forms;
using NSail.Localization;

namespace NSail.Components;

// The declared half of an open row's own rule, asked by the host that holds the row when the
// document's submit settles it (IFormRow.Settle): the same walk and the same wording
// NsDataAnnotationsValidator uses on the whole document, scoped to one row.
//
// It is asked NOW rather than left to the document's own pass, because a row that passes is
// about to UNMOUNT: the ambient validator would then discover the refusal on a field nobody is
// listening to any more, and the submit would answer false with nothing on screen to explain it
// (nsail#1029). Both row hosts ask it here, so what a row owes its own declarations cannot come
// to mean two things.
static class RowAnnotations
{
    // Posted on the context the row's FIELDS read — the form's where the row stands inside one —
    // so the refusal draws under the input with no second cascade. A row no member opened with
    // [Validated] is nobody's to enforce, here or on the wire, and refuses nothing.
    public static bool Refuses(object? document, object row, EditContext context, ValidationMessageStore messages, StringManager strings)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(messages);

        if (!ValidatedModels.Covers(document, row))
        {
            return false;
        }

        var refused = false;

        foreach (var model in ValidatedModels.Reach(row))
        {
            var validation = new ValidationContext(model);
            var results = new List<ValidationResult>();

            if (!Validator.TryValidateObject(model, validation, results, validateAllProperties: true))
            {
                refused = true;
            }

            foreach (var result in results)
            {
                foreach (var memberName in result.MemberNames.DefaultIfEmpty(string.Empty))
                {
                    messages.Add(new FieldIdentifier(model, memberName), RefusalWords.For(strings, model, memberName, result));
                }
            }
        }

        if (refused)
        {
            context.NotifyValidationStateChanged();
        }

        return refused;
    }
}
