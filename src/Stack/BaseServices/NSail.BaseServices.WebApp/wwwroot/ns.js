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
        // document from inside one -- except in a COMPOSER (NsSendMessage), where the words
        // being typed ARE the message and nothing else is being written: there Enter sends and
        // Shift+Enter writes the line, which is what every chat on earth has taught the hand.
        // The modified keystroke never reaches here, so the Shift half is the policy standing
        // down and leaving the browser its own newline.
        if (target.tagName === "TEXTAREA") {
            const composer = target.closest(".ns-send");

            // A candidate being composed (an accent held, a CJK pick) ends on an Enter that
            // belongs to the word: the person is still writing, and the message is not sent
            // under them. Read per keystroke, because composing is a state of the keystroke.
            if (!composer || e.isComposing)
                return;

            // The plain Enter is never a LINE in a composer -- Shift+Enter is, and it never
            // reaches here -- so the key either sends or does nothing at all, and an empty box
            // does not answer it with a blank line nobody asked for.
            e.preventDefault();

            // What it presses is the composer's own send, so a blank box and a send already in
            // flight refuse the key for the single reason they grey that face out
            // (NsSendMessage's blank rule, NsSubmit's FormRunning) -- no second rule here to
            // come apart from it.
            const send = composer.querySelector('button[type="submit"]');

            if (send && live(send))
                activate(send);

            return;
        }

        // THE MAP's Enter: the box in the drawer filters the app's own entries (NsNavMenu), and
        // Enter opens the first one still shown -- the palette standard, and literally that
        // entry's own link, so the key goes where the click would. The entries are read at the
        // instant it is pressed, so what "the first one" means is whatever the filter left; an
        // entry no hand could reach is not one, which is also what makes this nothing on a
        // phone with the drawer closed. Prevented either way: the box stands in no form, and a
        // filtered map with no match is not a page to reload.
        if (target.closest(".ns-nav-find")) {
            e.preventDefault();

            // The LINK inside the marked entry: the vendor hangs a class handed to its nav link
            // on the row's wrapper and not on the anchor, and a wrapper is not a thing a click
            // navigates.
            //
            // Where the filter left no entry at all the one row drawn is the app's own way on
            // with what was typed (NsNavMenu's NoMatch), and it is pressed by the same key: its
            // control is a BUTTON as often as a link -- taking a question somewhere is not
            // navigating -- so both shapes are named. Never both lists at once: the row exists
            // only while zero entries do, so document order is still "the first one shown".
            const entry = Array.from(document.querySelectorAll(
                ".ns-nav-entry a[href], .ns-nav-nomatch button, .ns-nav-nomatch a[href]")).find(live);

            if (entry)
                entry.click();

            return;
        }

        // A GRID's own Enter: the row is confirmed and the cursor keeps going down the column
        // being typed in -- the next row opened for it where the host opens one row at a time,
        // and a new row where there is none below, which is a counter writing a sale line after
        // line without reaching for the mouse. Never the document: a grid is a FIELD of the
        // document it stands in, and confirming a line is not saving a sale.
        const grid = target.closest(".ns-cells");
        const row = grid ? target.closest(CELL_ROW) : null;

        if (row) {
            e.preventDefault();
            advanceCells(grid, target, row, 1, true);
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
    // `step` is which way the cursor goes and `adding` whether running out of rows means making
    // one: Enter walks DOWN and writes the next line where there is none, and the arrows walk
    // either way and stop at the ends -- a grid is not a ring.
    async function advanceCells(grid, from, row, step, adding) {
        const cell = from.closest("td");
        const column = cell ? cell.cellIndex : -1;
        const at = rowsOf(grid).indexOf(row);

        if (at < 0)
            return;

        await frame();

        const commit = rowAt(grid, at)?.querySelector(".ns-row-commit");

        // Leaving an open row is CLOSING it, and the only thing that may close it is the row's
        // own Confirmar: a host that opens one row at a time greys every other row's pencil
        // while one is open, so a move that skipped this would have nothing to move onto -- and
        // a second mechanism for closing a row is exactly what this does not have.
        if (commit) {
            commit.click();

            // A row the host REFUSED stays open with its reason on it, and the cursor stays in
            // it: the move is what the confirm earned, not what the keystroke asked for.
            const closed = await until(() => !rowAt(grid, at)?.querySelector(".ns-row-commit"));

            if (!closed)
                return;
        }

        if (rowAt(grid, at + step)) {
            await placeInRow(grid, at + step, column);
            return;
        }

        if (!adding)
            return;

        const add = addControl(grid);

        if (!add || !live(add))
            return;

        add.click();

        const field = await until(() => cellField(grid, rowsOf(grid).length - 1, -1));

        if (field)
            place(field);
    }

    // THE GRID's own arrows and its Delete, beside the Enter policy above and for its reason:
    // which row the cursor is on is a property of the KEYSTROKE, read off the DOM at the instant
    // it is pressed, and the press is literally that row's own control.
    //
    // Up and Down are the MOVE Enter makes, either way and stopping at the ends: a grid is not a
    // ring, so the row above the first and the row below the last are nowhere, and only Enter
    // writes a line that is not there yet. Left and Right are not here at all -- inside a cell
    // being typed in they are the caret's, which is what the ARIA grid pattern hands the control
    // in edit mode, and Tab is what crosses cells.
    //
    // Delete presses the row's own Delete, with whatever confirmation that button already
    // carries, and only while the cursor is NOT in a box: there the key is the character the
    // hand meant, the same WCAG 2.1.4 ground "/" stands on.
    function onCellsKeyDown(e) {
        if (e.defaultPrevented || e.isComposing || e.ctrlKey || e.metaKey || e.altKey || e.shiftKey)
            return;

        if (e.key !== "ArrowUp" && e.key !== "ArrowDown" && e.key !== "Delete")
            return;

        const target = e.target;

        if (!target || typeof target.closest !== "function" || typeof target.getAttribute !== "function")
            return;

        const grid = target.closest(".ns-cells");
        const row = grid ? target.closest(CELL_ROW) : null;

        if (!row)
            return;

        const field = target.getAttribute("data-ns-field") === "true";

        if (e.key === "Delete") {
            if (field || typing())
                return;

            const remove = row.querySelector(".ns-row-delete");

            if (!remove || !live(remove))
                return;

            e.preventDefault();
            remove.click();
            return;
        }

        // The arrows move the cursor between BOXES, so they are the move only where there is a
        // cursor to move: a focused button inside the row keeps the platform's own arrow, and a
        // text area's arrows are its lines. An open list owns them outright -- they walk its
        // options, which is onDocumentKeyDown's own aria-expanded read.
        if (!field || target.tagName === "TEXTAREA")
            return;

        if (target.getAttribute("role") === "combobox" && target.getAttribute("aria-expanded") === "true")
            return;

        // The keystroke is taken OUTRIGHT, propagation and all, which the two listeners above
        // never need to do: a number box steps its own value on these two keys and it does that
        // from its own handler rather than as the keydown's default action, so preventing the
        // default alone left the row it was leaving incremented by one. Capture plus this is
        // what keeps the key from reaching the box at all. A spinner's arrows are still on the
        // box for a pointer; inside a grid the keys are the grid's.
        e.preventDefault();
        e.stopPropagation();
        e.stopImmediatePropagation();

        advanceCells(grid, target, row, e.key === "ArrowDown" ? 1 : -1, false);
    }

    document.addEventListener("keydown", onCellsKeyDown, true);

    // The cursor's own step onto another row, down the column being typed in. Where the host
    // opens one row at a time that step IS opening the row for editing -- the pencil's own code
    // again, so the host keeps deciding what opening a row means. A grid whose every row is
    // already a row of boxes has no pencil and nothing to open: the cursor just moves.
    async function placeInRow(grid, index, column) {
        const pencil = rowAt(grid, index)?.querySelector(".ns-row-edit");

        if (pencil && live(pencil)) {
            pencil.click();
            await until(() => cellField(grid, index, column));
        }

        const field = cellField(grid, index, column);

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
    //
    // `character` is the WCAG 2.1.4 question and not the modifier one: a chord typed as a
    // character must not act while somebody is writing one, which is "/" and "?" -- while F2
    // and F3 write nothing, so they answer from inside a box too, which is the whole reason the
    // desktop spends them on edit and find.
    const CHORDS = [
        { chord: "Ctrl+Enter", key: "enter", modified: true, character: false },
        { chord: "Ctrl+S", key: "s", modified: true, character: false },
        { chord: "Ctrl+K", key: "k", modified: true, character: false },
        { chord: "/", key: "/", modified: false, character: true },
        { chord: "?", key: "?", modified: false, character: true },
        { chord: "F2", key: "f2", modified: false, character: false },
        { chord: "F3", key: "f3", modified: false, character: false }
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

    // WCAG 2.1.4: a chord that IS a character must not act while the person is typing, or "/"
    // and "?" are characters they can never write. role=combobox covers the search box of a lookup, which is
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
    //
    // A CLOSED DRAWER is the one surface none of those three answer for: it is not taken out of
    // the document and not hidden, it is slid off the side of the screen, so the map's own
    // search box behind a closed nav drawer would otherwise take the cursor where nobody can
    // see it and a filtered menu nobody can read would navigate on Enter. The vendor's own word
    // for the state, named here for WHISPER_SELECTOR's reason -- the price of the two languages
    // is that this one is kept in step by hand.
    function live(el) {
        return el.offsetParent !== null
            && !el.disabled
            && el.getAttribute("aria-disabled") !== "true"
            && el.closest(".mud-drawer--closed") === null;
    }

    function chordTarget(chord) {
        const declared = Array.from(document.querySelectorAll("[data-ns-keys]"))
            .filter(el => (el.getAttribute("data-ns-keys") || "").split(" ").includes(chord) && live(el));

        const focused = document.activeElement;
        const closest = focused && typeof focused.closest === "function" ? selector => focused.closest(selector) : () => null;

        // The grid ROW the cursor is on owns the chord before the form around it: every row
        // declares the same F2 on its own pencil, so the one that answers has to be the row the
        // hand is on and not whichever happens to be last on the page. First inside the row
        // because a row nests no surface -- its own Editar is the one in front of the person.
        const row = closest(CELL_ROW);
        const inRow = row ? declared.filter(el => row.contains(el)) : [];

        if (inRow.length > 0)
            return inRow[0];

        // The form the cursor is standing in owns the chord, whatever else is on screen: a
        // person pressing Ctrl+Enter inside an aside's form means that form's submit and not
        // the one on the page behind it.
        const form = closest("form");
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

        // A SUBMIT is pressed through its form, naming itself the submitter, which is what a
        // click on it means. Not by the click itself: a click dispatched from script runs the
        // button's own handler to completion inside the dispatch -- renderer batch included --
        // so a hero that greys itself out while the form runs (NsSubmit, FormRunning) is
        // already disabled when the click's activation behaviour would have submitted, and the
        // browser skips it. The chord then pressed a button that did nothing, silently.
        if (el.form && el.type === "submit") {
            el.form.requestSubmit(el);
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

        if (CHORDS.find(c => c.chord === chord).character && typing())
            return;

        const target = chordTarget(chord);

        if (!target)
            return;

        e.preventDefault();
        activate(target);
    }

    document.addEventListener("keydown", onChordKeyDown, true);

    // THE "?" SHEET's one source: the claims that are on screen at the instant it is asked,
    // which is the same set the dispatcher above would answer from -- so there is no list of
    // shortcuts for anybody to keep in step with a screen, here either. Each line is the
    // declaring control's OWN word, already drawn in the install's language, and the group is
    // where that control stands rather than anything it declares.
    //
    // Shaped as the three groups and not as a flat list with a label on each: the sheet's
    // headings are the house's and in its order, and a fourth group is not a thing this can
    // hand back. Deduplicated by the pair, because every row of a grid claims the same F2 and a
    // sheet saying so twenty times says nothing.
    function chords() {
        const groups = { navigation: [], forms: [], grids: [] };

        for (const el of document.querySelectorAll("[data-ns-keys]")) {
            if (!live(el))
                continue;

            const keys = el.getAttribute("data-ns-keys") || "";
            const act = actOf(el);

            if (keys.length === 0 || act.length === 0)
                continue;

            // A grid stands INSIDE a form, so the narrower seat is asked first.
            const group = el.closest(".ns-cells") ? groups.grids
                : el.closest("form") ? groups.forms
                : groups.navigation;

            if (!group.some(line => line.keys === keys && line.act === act))
                group.push({ keys, act });
        }

        return groups;
    }

    // The control's own word for what it does. data-ns-whisper is the bubble's "Word (Chord)"
    // and the word is what stands in front of the key; aria-label is the name every glyph-only
    // control carries anyway. A BOX has no word of its own and is named by its label -- the one
    // the person reads beside it, which is the placeholder where there is no label.
    function actOf(el) {
        const whispered = el.getAttribute("data-ns-whisper");
        const named = el.getAttribute("aria-label");

        if (whispered) {
            // The key is cut off the end, and only where there is a key to cut: the same
            // attribute is now written whether or not the control claims a chord, and a Label
            // that ends in brackets of its own is a word, not a badge. A control that DECLARED
            // the chord is the only one whose trailing "(...)" the house appended.
            const cut = el.hasAttribute("data-ns-keys") && whispered.endsWith(")")
                ? whispered.lastIndexOf(" (")
                : -1;

            return (cut > 0 ? whispered.slice(0, cut) : whispered).trim();
        }

        if (named)
            return named.trim();

        if (el.matches("input, textarea"))
            return (el.labels?.[0]?.textContent || el.getAttribute("placeholder") || "").trim();

        return (el.textContent || "").trim();
    }

    // THE WHISPER (ns-mud.css, ".ns-whisper"): the long form of something already on screen,
    // or the whole name of a control that draws none. One element in the browser's TOP LAYER,
    // placed from here, because all three of the bubble's rules are facts only the document can
    // state and none of them can be kept by drawing it inside the control it names: a bubble
    // inside the control is clipped by the nearest scrolling ancestor (NsPanel's own content
    // box), it extends that ancestor's scrollable area for as long as it shows (nsail#2197),
    // and it paints in the control's stacking context, which is under the panel beside it
    // (nsail#2540). The top layer answers all three at once -- nothing clips it, it is in no
    // scroll box to grow, and it is over every dialog and drawer without naming a z-index that
    // has to outrank theirs.
    //
    // THE DELAY IS THE WHOLE OF THE BUBBLE'S TIMING, here and nowhere else: a name that opens
    // on the way past is noise, so it opens only after a steady dwell and closes the instant
    // anything else happens. pointerenter does not bubble, so the capture phase is the only way
    // one document listener reaches every trigger; focusin bubbles and answers for the keyboard,
    // with :focus-visible telling a keyboard's focus from the one a press left behind.
    const WHISPER_DELAY = 2500;
    const WHISPER_GAP = 4;

    // Which controls may whisper: the ones that declared the text to say (NsButton, NsLink,
    // NsSubmit, NsClose, NsChip, NsTooltip), plus the two faces the VENDOR draws, where nothing
    // of ours can put an attribute and the accessible name is the whole of what there is to say
    // anyway -- a menu's icon face and a lookup's end adornment.
    const WHISPER_SELECTOR = '[data-ns-whisper]:not([data-ns-whisper=""]), .ns-menu > .mud-icon-button[aria-label]:not([aria-label=""]), .mud-autocomplete .mud-input-adornment-icon-button[aria-label]:not([aria-label=""])';

    let whisperBubble = null;
    let whisperTarget = null;
    let whisperTimer = 0;

    function whisperText(el) {
        return (el.getAttribute("data-ns-whisper") || el.getAttribute("aria-label") || "").trim();
    }

    // THE DOUBLE CARTEL IS DERIVED, never gated: a bubble repeating a word the control already
    // draws is the one thing the whisper must never be, and whether that word is on screen is a
    // question about the RENDERED control. innerText answers it for a label a container query
    // hid, one the footer's emphasis ladder hid, and one that was never written, with no
    // breakpoint restated anywhere and nothing to keep in step. Case-folded because a vendor
    // button uppercases its own label in CSS and innerText reports what is painted.
    function whisperAdds(el, text) {
        const shown = (el.innerText || "").replace(/\s+/g, " ").trim();

        return shown.toLocaleLowerCase() !== text.toLocaleLowerCase();
    }

    // "manual", never "auto": an auto popover joins the browser's own light-dismiss stack, and
    // opening one closes every auto popover that is not its ancestor -- a bubble naming a button
    // inside an open menu would take the menu down with it.
    //
    // isConnected and not merely "have I built one": the box is appended to the BODY, and a body
    // the router replaced wholesale takes it with it. showPopover on a detached element throws,
    // so the question is asked every time rather than once at startup.
    function whisperBox() {
        if (whisperBubble && whisperBubble.isConnected)
            return whisperBubble;

        whisperBubble = null;

        whisperBubble = document.createElement("div");
        whisperBubble.className = "ns-whisper";
        whisperBubble.setAttribute("role", "presentation");

        if ("popover" in whisperBubble)
            whisperBubble.popover = "manual";

        document.body.appendChild(whisperBubble);

        return whisperBubble;
    }

    function openWhisper(el) {
        const text = whisperText(el);

        if (!el.isConnected || text.length === 0 || !whisperAdds(el, text))
            return;

        const box = whisperBox();

        box.textContent = text;
        box.classList.add("ns-whisper-open");

        if (typeof box.showPopover === "function" && !box.classList.contains("ns-whisper-shown")) {
            box.showPopover();
            box.classList.add("ns-whisper-shown");
        }

        placeWhisper(el, box);
    }

    // Down is the general case and the flip is for the control with no room under it -- a flip
    // and never a squeeze: the bubble is measured where it already stands, so nothing is grown
    // to make it fit. Both axes are clamped to the viewport afterwards, which is what answers
    // for a control at the very edge of the window.
    function placeWhisper(el, box) {
        const anchor = el.getBoundingClientRect();
        const bubble = box.getBoundingClientRect();
        const below = anchor.bottom + WHISPER_GAP;
        const top = below + bubble.height > window.innerHeight - WHISPER_GAP
            ? anchor.top - WHISPER_GAP - bubble.height
            : below;
        const left = anchor.left + (anchor.width - bubble.width) / 2;

        box.style.top = `${clamp(top, WHISPER_GAP, window.innerHeight - bubble.height - WHISPER_GAP)}px`;
        box.style.left = `${clamp(left, WHISPER_GAP, window.innerWidth - bubble.width - WHISPER_GAP)}px`;
    }

    function clamp(value, low, high) {
        return Math.max(low, Math.min(value, Math.max(low, high)));
    }

    function closeWhisper() {
        clearTimeout(whisperTimer);
        whisperTimer = 0;
        whisperTarget = null;

        if (!whisperBubble)
            return;

        whisperBubble.classList.remove("ns-whisper-open");

        if (whisperBubble.classList.contains("ns-whisper-shown")) {
            whisperBubble.classList.remove("ns-whisper-shown");

            if (whisperBubble.isConnected) {
                whisperBubble.hidePopover();
            }
        }
    }

    function armWhisper(target, keyboard) {
        if (!target || typeof target.closest !== "function")
            return;

        const el = target.closest(WHISPER_SELECTOR);

        if (el === whisperTarget)
            return;

        // A bubble never outlives the trigger the pointer left: arriving anywhere that whispers
        // nothing is the second witness to that, beside the leave above, and it is the one that
        // does not depend on a node Blazor replaced still being there to fire one.
        if (!el) {
            closeWhisper();

            return;
        }

        // A Tab run through a toolbar passes over a dozen controls and none of them is the one
        // being read, which is the same thing a pointer crossing the row says -- so the keyboard
        // takes the same dwell. A control still holding focus after its own click is not asking
        // for its name, which is what :focus-visible is read for, and it is asked of the element
        // that TOOK the focus rather than of the trigger: a trigger may CONTAIN the focusable
        // one (NsTooltip's host span, a chip whose close button is what Tab reaches), and a
        // wrapper is never itself :focus-visible.
        if (keyboard && !target.matches(":focus-visible"))
            return;

        closeWhisper();
        whisperTarget = el;
        whisperTimer = setTimeout(() => openWhisper(el), WHISPER_DELAY);
    }

    document.addEventListener("pointerenter", e => armWhisper(e.target, false), true);
    document.addEventListener("focusin", e => armWhisper(e.target, true));

    // The pointer leaving the trigger, read in the capture phase for the same reason the enter
    // is: pointerleave does not bubble, and it fires on every ancestor the pointer left as well
    // as on the trigger itself, so the one that matters is recognised rather than assumed.
    document.addEventListener("pointerleave", e => {
        if (whisperTarget && e.target instanceof Node && e.target.contains(whisperTarget))
            closeWhisper();
    }, true);

    // Every other exit, and these are all of them: a press (what was pressed is opening, and the
    // name of what pressed it is not what anyone is reading), a key, a scroll ANYWHERE -- the
    // bubble is placed against a rectangle that just moved, and a tooltip that follows a scroll
    // is a tooltip nobody asked for a second time -- and the window losing the pointer or
    // changing size.
    for (const exit of ["pointerdown", "pointercancel", "click", "keydown", "wheel"])
        document.addEventListener(exit, closeWhisper, true);

    document.addEventListener("scroll", closeWhisper, { capture: true, passive: true });
    document.addEventListener("focusout", closeWhisper, true);
    window.addEventListener("blur", closeWhisper);
    window.addEventListener("resize", closeWhisper);
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

    // What the router (NsArrivalFocus) hands the cursor to on a navigation's arrival: the first
    // element on screen carrying the marker a field's own AutoFocus writes (NsFieldBase), and
    // only the screen's own name where there is none -- the h1 its title row draws (NsTitleBar),
    // which carries tabindex -1 precisely so this call can land on it. Never a comma selector,
    // because querySelector answers the first match in DOCUMENT ORDER and the screen's name
    // always comes before whatever a screen marks, so the marker would lose the one selector
    // list that tried both.
    //
    // A marker rendered after this very call -- a list still reading, a tab's panel -- is not
    // yet a lost cause: this waits for it (a MutationObserver, bounded by a timeout that falls
    // back to the screen's name), rather than reading the DOM once and giving up, which is what
    // left the vendor's own first-render-only AutoFocus unable to reach it in the first place.
    const ARRIVAL_MARK = '[data-ns-arrival-focus]:not([disabled]):not([aria-readonly="true"])';
    const ARRIVAL_FALLBACK_WAIT_MS = 1000;

    let arrivalGeneration = 0;
    let arrivalObserver = null;

    function arrivalFocus(fallbackSelector) {
        const generation = ++arrivalGeneration;

        disconnectArrival();

        // A touch screen's keyboard rises the instant a TEXT BOX takes focus, over the very
        // list the cursor was handed to see -- a device with a physical keyboard, or a pointer
        // that only hovers over one (a mouse, a trackpad), never pays that cost. The cost is
        // the box's, not the arrival's: a heading raises no keyboard, so a coarse pointer skips
        // the marker's pass alone and the screen's name still takes the cursor, which is how a
        // screen reader on a phone hears the page it just landed on (ui/focus.md, Touch).
        const marked = !window.matchMedia("(pointer: coarse)").matches;

        // Where the cursor stands as the arrival lands, which is the gesture that brought the
        // person here -- the link or the menu item they pressed, or nobody at all once the page
        // that held it is gone. It is what tells "untouched since we landed" from "a hand
        // reached for something of its own while we waited" below.
        const entry = document.activeElement;

        // The first, synchronous try always wins, exactly as the vendor's own unconditional
        // focus does; only from the moment this call starts WAITING does a hand that got there
        // first keep what it took (settle below).
        const present = marked ? findArrivalMarker() : document.querySelector(fallbackSelector);
        if (present) {
            land(present, marked);
            return;
        }

        let settled = false;
        const timer = setTimeout(function () {
            settle(document.querySelector(fallbackSelector), false);
        }, ARRIVAL_FALLBACK_WAIT_MS);

        // Nothing to watch for where the marker is not wanted: what is left to wait for is the
        // screen's own name, and the timeout above is the one that asks for it.
        if (marked) {
            arrivalObserver = new MutationObserver(function () {
                const late = findArrivalMarker();
                if (late)
                    settle(late, true);
            });
            arrivalObserver.observe(document.body, { childList: true, subtree: true });
        }

        // The guard a later arrival needs is generation, not merely "has this settled" -- a
        // second navigation landing inside the wait window must never let this one's fallback
        // steal the cursor from whatever the newer arrival already placed it on.
        function settle(el, isMarker) {
            if (settled || generation !== arrivalGeneration)
                return;

            settled = true;
            clearTimeout(timer);
            disconnectArrival();

            // Between the marker not being there yet and it (or the fallback) finally landing,
            // a person may have reached for something themselves -- the one window this call
            // must never win against, unlike its own first, synchronous try above.
            if (el && !taken())
                land(el, isMarker);
        }

        // Asked as "has the cursor MOVED since we landed", not as reached()'s list of shapes a
        // click can focus: inside this window a tab strip's div, a clickable grid row or a
        // tree's node is a place a hand deliberately went and none of them is in that list,
        // while the link that brought the person here IS -- so the list answers backwards at
        // both ends. Nobody holding it (the page that held the gesture is gone) reads as
        // untouched, which is what keeps the arrival working for the ordinary navigation.
        // reached() stays what it is for focusFirst, whose question is a different one: whether
        // the cursor is on a control at all, or on the focus trap's own landing div.
        function taken() {
            const now = document.activeElement;

            return now !== null
                && now !== entry
                && now !== document.body
                && now !== document.documentElement;
        }
    }

    // The marker is brought INTO VIEW the way any focus does: it is the box the person is about
    // to type in, and a list's search can sit below the fold on a short window. The screen's own
    // name is the opposite -- it draws no ring (ns-mud.css excludes tabindex -1), so scrolling to
    // it reveals nothing at all, and it stands at the top of the document, which is itself the
    // scroller (NsMainLayout gives the content no overflow of its own). Its pass can also land a
    // full second after the arrival (ARRIVAL_FALLBACK_WAIT_MS), and taken() does not see a wheel:
    // whatever the view moved to in that second -- the guide scrolling to the section a cold
    // /guide/{chapter}#{slug} names, a notch down a dashboard taller than the window -- is a
    // person's or a screen's own doing, and a scrolling focus would take it back to the top
    // (Vigía, nsail#2338 round 3).
    function land(el, isMarker) {
        el.focus({ preventScroll: !isMarker });
    }

    function findArrivalMarker() {
        for (const candidate of document.querySelectorAll(ARRIVAL_MARK)) {
            if (candidate.offsetParent !== null)
                return candidate;
        }

        return null;
    }

    function disconnectArrival() {
        if (arrivalObserver) {
            arrivalObserver.disconnect();
            arrivalObserver = null;
        }
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

    // A file the C# side produced, handed to the browser as a download. The stream reference
    // arrives as an object with arrayBuffer(), so the bytes cross once and never as base64 --
    // which for a spreadsheet of fifty thousand rows is a third of the payload saved and a
    // string the size of the file not built.
    //
    // The anchor is in the document before it is clicked: a detached element's click is ignored
    // outright by Firefox. And revokeObjectURL is deferred rather than immediate -- Safari reads
    // the URL after the click returns, and revoking it in the same turn leaves the download
    // named and empty.
    async function download(name, type, data) {
        const url = URL.createObjectURL(new Blob([await data.arrayBuffer()], { type }));
        const anchor = document.createElement("a");

        anchor.href = url;
        anchor.download = name;
        anchor.style.display = "none";

        document.body.appendChild(anchor);
        anchor.click();
        anchor.remove();

        setTimeout(() => URL.revokeObjectURL(url), 10000);
    }

    window.nsapp = { appReady, onAppReady, scrollToFraction, scrollToId, fractionAt, focusFirst, arrivalFocus, chords, recall, remember, download };
})();