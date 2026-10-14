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

    // THE SKIP LINK lands the CURSOR on the content and not merely the scroll (nsail#2207).
    // A fragment link does that by itself in a plain document -- the browser focuses the target
    // when it can hold focus, which is what NsMainLayout's tabindex is for -- but not in this
    // one: Blazor intercepts a same-page address and routes it itself, and its own handling
    // scrolls without moving focus. So the gesture is completed here, where the element is,
    // rather than through an interop call the layout would have to make for a link that already
    // names its target.
    //
    // Bubble phase and after the fact: nothing about the navigation is being prevented, and a
    // click the capture listener above stood down from is still this one's.
    function onSkipClick(e) {
        const target = e.target;

        if (!target || typeof target.closest !== "function")
            return;

        const skip = target.closest(".ns-skip");

        if (!skip)
            return;

        const content = document.getElementById((skip.getAttribute("href") || "").replace(/^#/, ""));

        if (content)
            content.focus();
    }

    document.addEventListener("click", onSkipClick);

    // THE ENTER POLICY, and the only code in the house that decides whether an Enter keystroke
    // may submit a form: Enter commits what is being typed and does what the control in front
    // of the person means, and a DOCUMENT is submitted by its own button or by Ctrl+Enter (the
    // keyboard layer below) -- never by a stray Enter in the middle of it. No page and no kit
    // carries a second answer, which is why nothing of this is written in markup:
    // `@onkeydown:preventDefault` is decided when the element renders, and every read below is a
    // property of the KEYSTROKE -- which list was open, which form the cursor was standing in --
    // read off the DOM at the instant it is pressed. Capture for onDocumentClick's reason as
    // well: first on the event's path, so no handler between the control and the document can
    // cost this its answer by stopping propagation.
    //
    // Three Enters are the browser's own and are left alone: a text area's is a new line, a
    // focused button's is its press -- the policy reaches for a box being typed into and for
    // nothing else, so the controls standing in the same form are pressed by the platform the
    // way they always were -- and a form that IS the question -- a sign-in, a filter bar, a box
    // standing alone -- keeps the implicit submission, which is what the last read below
    // measures by asking the form how many fields it renders.
    function onDocumentKeyDown(e) {
        if (e.defaultPrevented || e.key !== "Enter")
            return;

        // A modified Enter is not this policy's: Ctrl+Enter is the document's own submit and is
        // answered by the control declaring it (the keyboard layer below).
        if (e.ctrlKey || e.metaKey || e.altKey || e.shiftKey)
            return;

        const target = e.target;

        if (!target || typeof target.getAttribute !== "function" || typeof target.closest !== "function")
            return;

        // Enter over an open list belongs to the list: it picks what is highlighted, and the
        // form the box stands in is not part of that gesture. The browser disagrees -- a text
        // input inside a form implicitly submits it on Enter -- and the two answers ride the
        // SAME keystroke: the submission is the keydown's default action, and the pick is made
        // from the keyup (MudAutocomplete.OnInputKeyUpAsync; its keydown answers Tab and the
        // arrows and nothing else). So the form was saved on the keystroke that was only
        // choosing an article. Preventing a keydown's default does not suppress the keyup after
        // it, so the pick is left exactly as it was and only the submission is taken away.
        //
        // `aria-expanded` is the platform's own word for "a list is open", read at the moment it
        // is asked rather than a mark the house would have to stamp on every combobox it wraps.
        // On a lookup the vendor writes it only while the list is open AND has options, which is
        // exactly the keystroke that picks. NsSelect and NsMultiSelect stamp the same two words
        // on their own input, so this names no component and needs no second condition.
        //
        // Scoped to the combobox ITSELF, never to whatever is expanded around it: an expander's
        // chevron and a menu's face are buttons that carry the same attribute, and a button's
        // Enter IS its activation -- preventing that would cost them the keyboard.
        if (target.getAttribute("role") === "combobox" && target.getAttribute("aria-expanded") === "true") {
            e.preventDefault();
            return;
        }

        // Everything below is about a box being TYPED INTO, which is what the field mark says
        // (NsFieldBase writes it on every field's input). An Enter that landed anywhere else
        // keeps whatever the platform gives it -- and on a focused BUTTON that is the button's
        // own activation, which is the keydown's default action: a row's pencil, its Confirmar,
        // a menu's face and the submit itself all stand inside the very form and the very grid
        // rows the two reads below answer for, so without this they would be pressed by nobody.
        if (target.getAttribute("data-ns-field") !== "true")
            return;

        // A text area's Enter is the line it writes, and Ctrl+Enter above is what submits the
        // document from inside one.
        if (target.tagName === "TEXTAREA")
            return;

        // A GRID's own Enter: the row is confirmed and the cursor keeps going down the column
        // being typed in -- the next row opened for it where the host opens one row at a time,
        // and a new row where there is none below, which is a counter writing a sale line after
        // line without reaching for the mouse. Never the document: a grid is a FIELD of the
        // document it stands in, and confirming a line is not saving a sale.
        const grid = target.closest(".ns-cells");
        const row = grid ? target.closest(CELL_ROW) : null;

        if (row) {
            e.preventDefault();
            advanceCells(grid, target, row);
            return;
        }

        // The box that ANSWERS Enter itself: a mailbox taken into a chip list, a scanned code
        // resolved into a line (NsTextFieldBase.TakesEnter, which stamps this). The keystroke
        // already means "the entry is finished", and the form saving the whole document on the
        // same one is the second meaning nobody asked for -- a counter passing ten boxes over a
        // reader would close the sale on the first.
        if (target.getAttribute("data-ns-commits") === "true") {
            e.preventDefault();
            return;
        }

        // And a field of a DOCUMENT: Enter ends the entry and does nothing else -- no submit and
        // no focus move, because Tab is what moves and Enter keeps one meaning per control. What
        // tells a document from a question is the form's own word for it (NsForm, which stamps
        // data-ns-tracked) together with how many fields it actually renders at this instant: a
        // tracked form of ONE field is a question too -- a dialog collecting an amount, a page's
        // single box -- and keeps the submission, which is where the industry's own split falls.
        const form = target.closest('form[data-ns-tracked="true"]');

        if (form && form.querySelectorAll('[data-ns-field="true"]').length > 1)
            e.preventDefault();
    }

    document.addEventListener("keydown", onDocumentKeyDown, true);

    // The grid's move, which is the one half of the policy that is not a prevention: the press is
    // literally the row's own control (.ns-row-commit, .ns-row-edit, .ns-cells-add, written by
    // NsTable and NsListEditor), so the key runs the code the button runs and the host keeps
    // deciding what a commit means and what a refused row does. What the key adds is the MOVE,
    // which is a DOM gesture and belongs on this side for focusFirst's reason.
    //
    // A frame before the first press: this listener answers in the capture phase, before the
    // keystroke has reached the box at all, and a click dispatched from here would be processed
    // ahead of the box's own commit -- the row confirmed from a value the keystroke had not
    // finished writing.
    async function advanceCells(grid, from, row) {
        const cell = from.closest("td");
        const column = cell ? cell.cellIndex : -1;
        const at = rowsOf(grid).indexOf(row);

        if (at < 0)
            return;

        await frame();

        const commit = rowAt(grid, at)?.querySelector(".ns-row-commit");

        if (commit) {
            commit.click();

            // A row the host REFUSED stays open with its reason on it, and the cursor stays in
            // it: the move is what the confirm earned, not what the keystroke asked for.
            const closed = await until(() => !rowAt(grid, at)?.querySelector(".ns-row-commit"));

            if (!closed)
                return;
        }

        if (rowAt(grid, at + 1)) {
            const pencil = rowAt(grid, at + 1).querySelector(".ns-row-edit");

            // Where the host opens one row at a time, going down the column is opening the row
            // below for editing -- the pencil's own code again. A grid whose every row is
            // already a row of boxes has no pencil and nothing to open: the cursor just moves.
            if (pencil && live(pencil)) {
                pencil.click();
                await until(() => cellField(grid, at + 1, column));
            }

            const field = cellField(grid, at + 1, column);

            if (field)
                place(field);

            return;
        }

        const add = addControl(grid);

        if (!add || !live(add))
            return;

        add.click();

        const field = await until(() => cellField(grid, rowsOf(grid).length - 1, -1));

        if (field)
            place(field);
    }

    // The way in stands in the grid's own bar, above the rows and outside them -- the one seat
    // Agregar has (ui/hosts.md) -- so it is a SIBLING of the rows and the search is made from
    // their parent. A page that draws the add control itself, out of its own chrome, declares
    // none of this: there the control adds to whichever collection the page is showing, and it
    // is not this grid's to press.
    function addControl(grid) {
        return grid.querySelector(".ns-cells-add")
            ?? grid.parentElement?.querySelector(".ns-cells-add")
            ?? null;
    }

    function frame() {
        return new Promise(resolve => requestAnimationFrame(resolve));
    }

    // What a press asked Blazor for is not in the DOM on the line after it -- the renderer
    // answers on its own circuit. Half a second of frames, after which the move is simply not
    // made: a keystroke never raises.
    async function until(find, frames) {
        for (let left = frames ?? 30; left > 0; left--) {
            const found = find();

            if (found)
                return found;

            await frame();
        }

        return null;
    }

    // A row of a grid, as either of the two hosts draws one (NsTable's body rows, NsListEditor's
    // items). A control inside the grid but outside every row -- a toolbar's own search box --
    // is not in this policy at all and keeps whatever Enter it had.
    const CELL_ROW = "tbody > tr, .ns-list-editor-item";

    // The rows in the order they are read. Read again on every step rather than held: each press
    // above is a render, and a row element the renderer replaced is an element nothing can be
    // focused inside.
    function rowsOf(grid) {
        return Array.from(grid.querySelectorAll(CELL_ROW));
    }

    function rowAt(grid, index) {
        return index >= 0 ? rowsOf(grid)[index] ?? null : null;
    }

    // The box the cursor goes to: the one in that column where the column has one, and the
    // row's first otherwise -- a list editor's row has no columns to count, and a column whose
    // cell holds a derived total or a locked value has nothing to type into.
    function cellField(grid, index, column) {
        const row = rowAt(grid, index);

        if (!row)
            return null;

        const cell = column >= 0 && row.cells ? row.cells[column] : null;

        return typable(cell) ?? typable(row);
    }

    function typable(scope) {
        if (!scope)
            return null;

        for (const candidate of scope.querySelectorAll(TYPABLE)) {
            if (candidate.offsetParent !== null)
                return candidate;
        }

        return null;
    }

    // THE KEYBOARD LAYER. A chord is declared by the control that runs it (NsKeys, which writes
    // data-ns-keys) and this layer does one thing with it: find the control on screen declaring
    // the chord just pressed and activate it. So the key runs exactly the code the click runs,
    // the badge on the button and the dispatch are one declaration, and a chord nothing on this
    // screen declares does nothing at all -- a control that is hidden, disabled or simply not
    // rendered takes its key with it, with no list of active shortcuts to keep in step.
    //
    // Written here and not in the markup for onDocumentClick's reason plus one of its own: the
    // control that answers a chord is never the element the keystroke landed on, so there is no
    // element whose own @onkeydown would ever see it. Capture for the same reason as the two
    // listeners above -- first on the path, so no handler between the focus and the document can
    // cost a chord its answer.
    //
    // Escape is NOT here: a routed overlay answers it itself (NsCloseOnEscape) and a hosted
    // dialog through the vendor's own CloseOnEscapeKey, both already closing through the guard
    // the X goes through. A close still DECLARES the chord, for the badge and for
    // aria-keyshortcuts; one dispatcher asking it again would close the surface twice.
    const CHORDS = [
        { chord: "Ctrl+Enter", key: "enter", modified: true },
        { chord: "Ctrl+S", key: "s", modified: true },
        { chord: "/", key: "/", modified: false }
    ];

    // Which chord a keystroke is, or nothing. Cmd counts as Ctrl: the chord is written in the
    // one spelling the badge draws, and a Mac's hand reaches for the key in its own place.
    // Shift and Alt are never part of a chord the house claims, so a keystroke carrying one is
    // somebody else's -- except where the key itself needs Shift to be typed at all ("?"),
    // which `key` already accounts for because it is the CHARACTER and not the physical key.
    function chordOf(e) {
        const modified = e.ctrlKey || e.metaKey;

        if (e.altKey || (modified && e.shiftKey))
            return null;

        const key = typeof e.key === "string" ? e.key.toLowerCase() : "";

        for (const candidate of CHORDS) {
            if (candidate.key === key && candidate.modified === modified)
                return candidate.chord;
        }

        return null;
    }

    // WCAG 2.1.4: a chord with no modifier must not act while the person is typing, or "/" is a
    // character they can never write. role=combobox covers the search box of a lookup, which is
    // a text field the browser does not type-check as one.
    function typing() {
        const el = document.activeElement;

        if (!el || typeof el.matches !== "function")
            return false;

        return el.matches('textarea, [contenteditable="true"], [role="combobox"], input:not([type="checkbox"]):not([type="radio"]):not([type="button"]):not([type="submit"]):not([type="file"])');
    }

    // A control is only a candidate while a hand could reach it: the chord does what the click
    // does, so what the click cannot do the key must not either. offsetParent for focusFirst's
    // reason -- a control on a tab NsTabs has not switched to is display:none and still in the
    // DOM -- and aria-disabled beside the attribute because the vendor's own buttons mark a
    // refusal both ways.
    function live(el) {
        return el.offsetParent !== null
            && !el.disabled
            && el.getAttribute("aria-disabled") !== "true";
    }

    function chordTarget(chord) {
        const declared = Array.from(document.querySelectorAll("[data-ns-keys]"))
            .filter(el => (el.getAttribute("data-ns-keys") || "").split(" ").includes(chord) && live(el));

        // The form the cursor is standing in owns the chord, whatever else is on screen: a
        // person pressing Ctrl+Enter inside an aside's form means that form's submit and not
        // the one on the page behind it.
        const focused = document.activeElement;
        const form = focused && typeof focused.closest === "function" ? focused.closest("form") : null;
        const inForm = form ? declared.filter(el => form.contains(el)) : [];

        if (inForm.length > 0)
            return inForm[inForm.length - 1];

        // The app's MAP is not the page: the nav drawer carries a search box of its own, and
        // "/" belongs to the search of the screen the person is reading.
        const onPage = declared.filter(el => !el.closest(".ns-nav-drawer"));
        const pool = onPage.length > 0 ? onPage : declared;

        // Last in document order, which is the innermost surface open: an overlay is rendered
        // after the page it stands over -- the aside's drawer and the dialog provider are both
        // later siblings of the main content (NsMainLayout) -- so a chord the page also
        // declares is answered by what is on top of it.
        return pool.length > 0 ? pool[pool.length - 1] : null;
    }

    // A box is activated by being ready to type in, never by being clicked: "/" means land the
    // cursor in the search, and the box selects its own content on focus (NsFieldBase).
    function activate(el) {
        if (el.matches("input, textarea")) {
            el.focus();
            return;
        }

        el.click();
    }

    function onChordKeyDown(e) {
        if (e.defaultPrevented || e.isComposing)
            return;

        const chord = chordOf(e);

        if (chord === null)
            return;

        // Ctrl+S opens the browser's own "save this page" dialog, and an app that answers the
        // chord must never let that open behind its save -- nor on a screen with no form at
        // all, where offering to file the HTML of a page is nobody's intention. Every other
        // chord keeps its default until something actually answers it.
        if (chord === "Ctrl+S")
            e.preventDefault();

        if (!CHORDS.find(c => c.chord === chord).modified && typing())
            return;

        const target = chordTarget(chord);

        if (!target)
            return;

        e.preventDefault();
        activate(target);
    }

    document.addEventListener("keydown", onChordKeyDown, true);

    // THE WHISPER's own flip (ns-mud.css, "THE WHISPER"): the bubble is attr(aria-label),
    // drawn by a ::after the CSS alone sets up, and CSS alone cannot ask whether there is room
    // below it -- so that is the one question answered here, nothing else about the bubble.
    // WHISPER_HEIGHT is read off its own fixed geometry (ns-mud.css: a 12px/1.4 line, 4px
    // padding top and bottom, the 4px gap before it) with a few px to spare -- it never wraps
    // (white-space: nowrap), so the number is exact rather than guessed. pointerenter does not
    // bubble, so the capture phase is the only way one document listener reaches every trigger;
    // focusin already bubbles and answers for the keyboard the same selectors' :focus-visible
    // gate does. The selector list is ns-mud.css's own — kept in step by hand, the price of
    // the two languages neither can read the other's.
    const WHISPER_SELECTOR = '.ns-square[aria-label]:not([aria-label=""]), .ns-menu > .mud-icon-button[aria-label]:not([aria-label=""]), .mud-autocomplete .mud-input-adornment-icon-button[aria-label]:not([aria-label=""]), .ns-collapse-sm[aria-label]:not([aria-label=""]), .ns-collapse-md[aria-label]:not([aria-label=""]), .ns-collapse-lg[aria-label]:not([aria-label=""]), .ns-collapse-xl[aria-label]:not([aria-label=""]), .ns-panel-footer .mud-button-filled:has(.mud-button-icon-start):not(.mud-button-filled-primary):not(.mud-button-filled-error)[aria-label]:not([aria-label=""])';
    const WHISPER_HEIGHT = 32;

    function placeWhisper(target) {
        if (!target || typeof target.closest !== "function")
            return;

        const el = target.closest(WHISPER_SELECTOR);
        if (!el)
            return;

        const fits = el.getBoundingClientRect().bottom + WHISPER_HEIGHT <= window.innerHeight;
        el.toggleAttribute("data-ns-whisper-above", !fits);
    }

    document.addEventListener("pointerenter", e => placeWhisper(e.target), true);
    document.addEventListener("focusin", e => placeWhisper(e.target));

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
    //
    // One answer to "a box the cursor may be put in", shared with the grid's own move: both are
    // placing a cursor in somebody's field, and two selectors would drift apart on the next
    // reason to skip a box.
    const TYPABLE = 'input:not([type="hidden"]):not([disabled]):not([aria-readonly="true"]), select:not([disabled]), textarea:not([disabled]):not([aria-readonly="true"])';

    function focusFirst(formId) {
        const root = document.getElementById(formId);
        if (!root)
            return;

        for (const candidate of root.querySelectorAll(TYPABLE)) {
            if (candidate.offsetParent === null)
                continue;

            place(candidate);

            // A popup's own focus trap places the cursor on the non-interactive fallback div it
            // wraps the dialog in, and it does that AFTER this call -- the field takes the cursor
            // and loses it in the same tick, which is the overlay div the sweep reported for
            // Conectar un Calendario (nsail#2000, traced in a browser: ours, then Blazor's
            // FocusAsync for the trap). The vendor's DefaultFocus option does not withdraw that
            // gesture -- measured, None behaves as Element does. The next frame is after the
            // trap, so the cursor is taken back there, once -- but ONLY from the trap's own
            // landing place, never from a control.
            //
            // That frame is not the 16ms it reads as: measured in Chromium against a real host,
            // the aside's render leaves it 490-640ms wide, which is a person's whole reach for
            // the box they are aiming at. Taking the cursor back off a box the hand had already
            // reached handed the graduation being typed to the lookup it was taken back to --
            // which opens its list on the TEXT, not on the focus the swallow above covers, so
            // "Crear Medico..." stood over a form nobody had touched it in (nsail#2169).
            requestAnimationFrame(function () {
                if (!reached(document.activeElement))
                    place(candidate);
            });

            return;
        }
    }

    // Whether the cursor is somewhere a gesture could have put it. The focus trap's fallback div
    // is not: it is a div with a tabindex and nothing a person can aim at, which is what makes it
    // tellable from the box, the picker or the button a hand reaches for. Asked of the shapes a
    // click can focus rather than of [tabindex] alone, because that is what the trap's own
    // landing place carries too.
    function reached(el) {
        return el !== null
            && typeof el.matches === "function"
            && el.matches('input, select, textarea, button, a[href], [contenteditable="true"]');
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