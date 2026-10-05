(function () {
    if (window.nsapp)
        return;

    let ready = false;
    let lastDetail = null;

    function appReady(detail) {
        if (ready)
            return;

        ready = true;
        lastDetail = detail ?? null;

        window.dispatchEvent(new CustomEvent("nsapp:app-ready", { detail: lastDetail }));
    }

    function onAppReady(handler, options) {
        if (ready) {
            handler(lastDetail);
            return () => { };
        }

        window.addEventListener("nsapp:app-ready", e => handler(e.detail), options);
        return () => window.removeEventListener("nsapp:app-ready", handler);
    }

    // Scrolls a scroll box so the given fraction of its content sits at `bias` of its visible
    // height from the top -- how a day view opens on "now" (bias .5, centred) or on the hour
    // it is framed at (bias 0, at the top) without the C# side ever knowing how tall an hour
    // is. The target is an element or a selector: a component with an @ref hands the element
    // it already holds, a page that only knows its own class hands the class.
    function scrollToFraction(target, fraction, bias) {
        var el = typeof target === "string" ? document.querySelector(target) : target;
        if (!el)
            return;
        var top = fraction * el.scrollHeight - el.clientHeight * (bias ?? 0.3);
        el.scrollTop = Math.max(0, top);
    }

    // Where inside an element a pointer landed, 0 at its top edge and 1 at its bottom -- the
    // read half of the same trade scrollToFraction makes, since how tall an element is on this
    // screen is the one thing the C# side cannot answer. An element with no height, or one that
    // is not there any more by the time the click is handled, answers 0: the top of it.
    function fractionAt(target, clientY) {
        var el = typeof target === "string" ? document.querySelector(target) : target;
        if (!el)
            return 0;
        var rect = el.getBoundingClientRect();
        if (!(rect.height > 0))
            return 0;
        return Math.min(1, Math.max(0, (clientY - rect.top) / rect.height));
    }

    // Where an anchor lands: the element carrying that id, brought into whatever box actually
    // scrolls around it. The browser's own fragment handling cannot do this one -- the address
    // carries the anchor before the document it names has been fetched, so the element the
    // browser looked for did not exist yet and the scroll it never made is not repeated.
    function scrollToId(id) {
        var el = document.getElementById(id);
        if (!el)
            return;
        el.scrollIntoView({ block: "start", behavior: "smooth" });
    }

    // A link whose destination only redraws a surface over the page the user is already on
    // drives itself: preventing its click's default is what stands Blazor's own link
    // interception down (it skips a click already defaultPrevented), so the move replaces the
    // history entry instead of pushing one. That prevention cannot be written in the markup --
    // `@onclick:preventDefault` is decided when the element renders, and whether a click
    // deserves to be prevented is a property of the click, not of the element. So the decision
    // is made here, on the same terms Blazor's own interceptor uses: a modifier or a
    // non-primary button means the user asked the BROWSER for this address -- open in a new
    // tab, a new window -- and the anchor still carries the whole address, surface query
    // included. The C# side reads the same flags and stands down for the same clicks
    // (NsLink.Follow), so the tab opens and nothing moves in place behind it.
    //
    // Capture on the document is deliberate: it is the first listener on the event's path, so
    // this answers before Blazor's own delegated listener no matter which registered first.
    function onDocumentClick(e) {
        if (e.defaultPrevented || e.button !== 0 || e.ctrlKey || e.shiftKey || e.altKey || e.metaKey)
            return;

        const target = e.target;

        if (!target || typeof target.closest !== "function")
            return;

        if (target.closest(".ns-link-intercept"))
            e.preventDefault();
    }

    document.addEventListener("click", onDocumentClick, true);

    // Enter over an open list belongs to the list: it picks what is highlighted, and the form
    // the box stands in is not part of that gesture. The browser disagrees -- a text input
    // inside a form implicitly submits it on Enter -- and the two answers ride the SAME
    // keystroke: the submission is the keydown's default action, and the pick is made from the
    // keyup (MudAutocomplete.OnInputKeyUpAsync; its keydown answers Tab and the arrows and
    // nothing else). So the form was saved on the keystroke that was only choosing an article.
    // Preventing a keydown's default does not suppress the keyup after it, so the pick is left
    // exactly as it was and only the submission is taken away.
    //
    // Written here and not in the markup for onDocumentClick's reason: `@onkeydown:preventDefault`
    // is decided when the element renders, and whether an Enter deserves preventing is a property
    // of the keystroke -- whether a list was open AT that instant. `aria-expanded` is that same
    // answer read from the DOM at the moment it is asked, and it is the platform's own word for
    // it rather than a mark the house would have to remember to stamp on every combobox it ever
    // wraps. On a lookup the vendor writes it only while the list is open AND has options, which
    // is exactly the keystroke that picks: on a closed, empty or already-settled box Enter is
    // still the form's, and a plain field in a one-field form still submits on Enter the way it
    // always did. NsSelect and NsMultiSelect stamp the same two words on their own input, so this
    // names no component and needs no second condition to cover them.
    //
    // Scoped to the combobox ITSELF, never to whatever is expanded around it: an expander's
    // chevron and a menu's face are buttons that carry the same attribute, and a button's Enter
    // IS its activation -- preventing that would cost them the keyboard. Capture for the click's
    // reason as well: first on the path, so no handler between the box and the document can cost
    // this its answer by stopping propagation.
    function onDocumentKeyDown(e) {
        if (e.defaultPrevented || e.key !== "Enter")
            return;

        const target = e.target;

        if (!target || typeof target.getAttribute !== "function")
            return;

        if (target.getAttribute("role") === "combobox" && target.getAttribute("aria-expanded") === "true")
            e.preventDefault();
    }

    document.addEventListener("keydown", onDocumentKeyDown, true);

    // The candidate `focusFirst` is about to call .focus() on, for the one capture-phase
    // listener below to recognise and swallow -- never read outside that listener.
    let suppressOpenFor = null;

    // MudAutocomplete's own input wires OnFocusAsync to `@onfocus` (MudBlazor 9.10.0), and
    // Blazor registers "focus" -- unlike "focusin" -- as one of its non-bubbling events: instead
    // of delegating from the bubble phase like a click, it listens on `document` WITH CAPTURE
    // (blazor.*.js's own EventDelegator), the instant a component with an `@onfocus` handler
    // first renders. That capture listener fires during the CAPTURING phase, before the event
    // ever reaches the input -- a listener on the input itself, capture or not, only runs at
    // AT_TARGET, strictly after. The only way to run first is another `document` capture
    // listener registered before Blazor's own, so this one is added once, unconditionally, by
    // this script tag -- which NsApp.razor places ahead of blazor.*.js in <head> -- rather than
    // lazily inside focusFirst: Blazor's lazy listener does not exist yet the first time any
    // focus fires on this page, and document.addEventListener runs listeners on the same
    // node+phase in registration order (Vigía, nsail#1815 round 3: a `focusin` swallow could
    // not touch this, because `focusin` fires after "focus" already opened the popover).
    document.addEventListener("focus", function (e) {
        if (e.target === suppressOpenFor) {
            suppressOpenFor = null;
            e.stopImmediatePropagation();
        }
    }, true);

    // The first editable control inside a freshly opened create form, so the person can type
    // at once instead of clicking into the page first. Scoped to the form's own id rather than
    // asked of `document` because a surface can hold more than one form (an aside over the main
    // page) and only the one that just opened owns this gesture. A hidden candidate -- a field
    // on a tab NsTabs has not switched to (`display:none`, never removed from the DOM) -- is
    // skipped by its own offsetParent rather than focused invisibly, and a field the screen
    // LOCKED (Editar Persona's Tipo) the same way a disabled one already was.
    //
    // What says "locked" is aria-readonly, which NsFieldBase writes for every read-only field
    // and nothing else writes -- NOT the plain `readonly` attribute, because a select's own
    // input carries that unconditionally: the vendor paints the chosen value into a text input
    // nobody types into, and a form whose first field is a select is exactly the shape the
    // cursor was walking past (nsail#2000).
    function focusFirst(formId) {
        const root = document.getElementById(formId);
        if (!root)
            return;

        const candidates = root.querySelectorAll(
            'input:not([type="hidden"]):not([disabled]):not([aria-readonly="true"]), select:not([disabled]), textarea:not([disabled]):not([aria-readonly="true"])');

        for (const candidate of candidates) {
            if (candidate.offsetParent === null)
                continue;

            place(candidate);

            // A popup's own focus trap places the cursor on the non-interactive fallback div it
            // wraps the dialog in, and it does that AFTER this call -- the field takes the cursor
            // and loses it in the same tick, which is the overlay div the sweep reported for
            // Conectar un Calendario (nsail#2000, traced in a browser: ours, then Blazor's
            // FocusAsync for the trap). The vendor's DefaultFocus option does not withdraw that
            // gesture -- measured, None behaves as Element does. The next frame is after the
            // trap, so the cursor is taken back there, once, and only where something really
            // moved it: on a page nothing does and this reads as a no-op.
            requestAnimationFrame(function () {
                if (document.activeElement !== candidate)
                    place(candidate);
            });

            return;
        }
    }

    // Only a MudAutocomplete's own input wires OpenOnFocus and reacts to THIS focus exactly like
    // a click, opening its popover over the page (Vigía, nsail#1815 round 2).
    // stopImmediatePropagation on the listener above halts the WHOLE dispatch, capture through
    // target, not just Blazor's own listener -- arming it for a plain field would cost that field
    // its own `onfocus="this.select()"` (NsFieldBase) for a popover it never opens (Vigía,
    // round 4). `.focus()` dispatches its events synchronously, so the flag set right before it
    // is read and cleared by the listener above before this call returns; nothing is left armed
    // for a focus that happens not to fire (the candidate was already the active element).
    //
    // role="combobox" alone is not that shape -- a select publishes it too
    // (NsComboboxExpandedMarkTests, which is why onDocumentKeyDown above reads it) and opens on a
    // mousedown, not on a focus. aria-autocomplete is the word that tells the box that SEARCHES
    // from the box that is merely chosen from: "list" against "none".
    function place(candidate) {
        if (candidate.getAttribute("aria-autocomplete") === "list")
            suppressOpenFor = candidate;

        candidate.focus();
        suppressOpenFor = null;
    }

    // What this machine remembers: localStorage, so it survives the tab and belongs to the
    // browser profile rather than to whoever is signed in. The values are already-serialized
    // strings -- the C# side owns the shape, this side owns nothing but the store.
    //
    // The try/catch is not defensive noise: reading localStorage THROWS outright where storage
    // is disabled or the quota is full (private browsing, a locked-down kiosk), so without it
    // a machine that cannot remember could not render the screen either.
    function recall(key) {
        try {
            return window.localStorage.getItem(key);
        } catch {
            return null;
        }
    }

    function remember(key, value) {
        try {
            window.localStorage.setItem(key, value);
        } catch {
        }
    }

    window.nsapp = { appReady, onAppReady, scrollToFraction, scrollToId, fractionAt, focusFirst, recall, remember };
})();