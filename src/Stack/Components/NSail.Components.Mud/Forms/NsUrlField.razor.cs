// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components;

public partial class NsUrlField
{
    // The box renders InputType.Url, and a `type="url"` input is refused by the browser for a
    // malformed address whatever its Required says — so a form that withdraws the browser's
    // constraint UI (NsForm's novalidate) withdraws this field's only format rule with it. It
    // belongs to the field rather than to each binding: a URL box is a URL box wherever it
    // stands, and the one call site in the house binds a member shared by every setting kind,
    // which can carry no declaration of its own.
    //
    // Posted as the field's own problem for the date box's reason (NsFieldBase.GetOwnProblem):
    // a refusal the person can see and the submit cannot is how a value nobody meant reaches
    // the server.
    //
    // And posted THERE ONLY — no GetErrorText of its own, which the date box and
    // MailCopyAddressesField can afford because their refusal is born of a keystroke and empty
    // until one. This one is read off Value on every render, so drawn straight it would refuse a
    // form the instant it opens over an address the model brought, and again on the first letter
    // of a scheme being typed. The moment belongs to NsFieldBase: the validation request Save
    // raises, and from then on the parameter set the binding's write triggers
    // (intentional-ui.md — Save, never the keystroke).
    protected override string? GetOwnProblem()
    {
        var typed = Value?.Trim();

        if (string.IsNullOrEmpty(typed))
        {
            return null;
        }

        if (Readable(typed))
        {
            return null;
        }

        // Two motives, because one sentence cannot name both: an address that never said
        // `scheme://` is incomplete, and one that said it and still cannot be read is broken
        // where the domain stands. Telling somebody to "write it whole" about
        // `https://acme example/x` names a motive that is not the defect.
        return Strings.Translate(NamesADomain(typed)
            ? "Problems.UnreadableAddressDomain"
            : "Problems.UnreadableAddress");
    }

    // The browser's own reading of a `type="url"`, which is "an absolute URL" and not "a web
    // page": any scheme will do and what it insists on is that there BE one, so `www.acme.com`
    // is refused while `https://acme.com/a b` is taken — Chromium's parser escapes the space,
    // and so does Uri. Matched to that in the path and the query rather than tightened, because
    // a value the browser let through is already saved in an install and a stricter box would
    // open its screen refused and hold every Guardar on it.
    //
    // Uri.IsWellFormedUriString is NOT that rule: it asks for the escaped canonical form, so it
    // refuses the space, the brace and the backslash Chromium normalizes (measured, both ends).
    // TryCreate alone is one step the other way — it reads `//acme.com/x` as a path and hands
    // back a scheme the text never carried — so the text must carry the scheme it parsed as.
    //
    // Where this is STRICTER than the gate it replaces, and the only place: a domain no parser
    // can read. Chromium takes `https://acme example/x`, `http://ac me.com`, `https:/acme.com`
    // and `C:\x\y` as valid `type="url"` values (measured, Chrome 154 and Playwright's own
    // build); Uri refuses all four. Kept refused on purpose — the one real call site is a link
    // that travels literally into a message somebody receives, so an address nothing can resolve
    // is worth the cost that invariant exists to avoid, and fields.md states that cost.
    static bool Readable(string typed)
    {
        return Uri.TryCreate(typed, UriKind.Absolute, out var address)
            && typed.StartsWith(address.Scheme + ':', StringComparison.OrdinalIgnoreCase);
    }

    // `scheme://` and anything after it: the text announced a domain, so whatever is unreadable
    // is there and not in the announcement. `https:/acme.com` and `C:\x\y` announce none.
    static bool NamesADomain(string typed)
    {
        var mark = typed.IndexOf("://", StringComparison.Ordinal);

        return mark > 0 && Uri.CheckSchemeName(typed[..mark]);
    }
}
