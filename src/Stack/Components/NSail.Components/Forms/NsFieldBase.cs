// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using System.Linq.Expressions;
using System.Text;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components;

public abstract class NsFieldBase<TValue> : ComponentBase, IDisposable
{
    EditContext? _subscribedEditContext;
    IFieldTracker? _tracker;
    FieldIdentifier? _tracked;
    FieldIdentifier? _fieldIdentifier;
    Expression<Func<TValue>>? _for;
    string? _label;
    bool _declared;
    ValidationMessageStore? _ownMessages;
    string? _posted;
    bool _asked;

    [Inject]
    protected StringManager Strings { get; init; } = default!;

    [Inject]
    protected MetadataProvider Metadata { get; init; } = default!;

    [CascadingParameter]
    protected EditContext? EditContext { get; set; }

    /// <summary>An ancestor (NsForm, NsTab) that wants to know which fields render inside it:
    /// a tab derives whether it currently holds a validation problem, a form whether an issue
    /// naming a member has a field to draw under.</summary>
    [CascadingParameter]
    protected IFieldTracker? FieldTracker { get; set; }

    /// <summary>What the field is called. Unset derives it from the binding expression; an
    /// explicitly BLANK string hides the label without removing it — the field keeps the
    /// derived text as its accessible name (see GetAccessibleName).</summary>
    [Parameter]
    public string? Label { get; set; }

    [Parameter]
    public TValue? Value { get; set; }

    [Parameter]
    public EventCallback<TValue?> ValueChanged { get; set; }

    [Parameter]
    public Expression<Func<TValue>>? ValueExpression { get; set; }

    [Parameter]
    public Expression<Func<TValue>>? For { get; set; }

    [Parameter]
    public bool Disabled { get; set; }

    [Parameter]
    public bool ReadOnly { get; set; }

    [Parameter]
    public string? Placeholder { get; set; }

    [Parameter]
    public string? Helper { get; set; }

    /// <summary>Marks the field required on top of whatever the bound member declares. It is
    /// the override for what a [Required] cannot express: a requirement that holds only
    /// sometimes (a Store's parent, while the Store is external) and a non-nullable binding
    /// whose "empty" is a sentinel RequiredAttribute never rejects (a Guid bound as Guid —
    /// Guid.Empty boxes non-null, which is what HandleValidationRequested below exists for).
    /// A member whose [Required] can actually refuse — a reference type, a Nullable&lt;T&gt; —
    /// needs none of it.</summary>
    [Parameter]
    public bool Required { get; set; }

    /// <summary>Whether the field draws as required. Derived from the bound member's own
    /// [Required], wherever that attribute is one that can refuse (RequiredMembers), and never
    /// from what a page repeated, so the mark cannot drift from the declaration it is
    /// about.</summary>
    protected bool IsRequired
    {
        get { return Required || _declared; }
    }

    [Parameter]
    public bool AutoFocus { get; set; }

    [Parameter]
    public bool Immediate { get; set; }

    /// <summary>Makes the field fill the free space of its flex parent (a toolbar's search
    /// box pushing the actions to the edge). Same word, same meaning as on NsTable/NsStack.</summary>
    [Parameter]
    public bool Grow { get; set; }

    protected string? GrowClass => Grow ? "flex-1" : null;

    /// <summary>The browser's autofill hint, verbatim from the HTML attribute (`off`,
    /// `new-password`, `email`…). It exists because a field that merely LOOKS like a login
    /// gets filled with the saved one: a credential asked for by a third party — an
    /// app-specific password, an API secret — arrives prefilled with the password for THIS
    /// site, which the person may then submit somewhere it does not belong. Free text on
    /// purpose (naming.md): the vocabulary is the HTML spec's, not ours.</summary>
    [Parameter]
    public string? Autocomplete { get; set; }

    /// <summary>What the underlying input needs splatted onto it — the autofill hint, the
    /// accessible name a hidden label leaves behind, the mostrador focus behaviour
    /// (SelectOnFocus) and the reference to the node carrying this field's refusal. Never null:
    /// the vendor's input reads this collection during initialization and throws on a null one.
    /// Never rebuilt per render either — a fresh dictionary every time reads as a changed
    /// parameter and re-renders the field for nothing — only when one of the values changes.
    ///
    /// Read here rather than refreshed in OnParametersSet: a refusal arrives from the
    /// EditContext, which re-renders the field without setting a parameter on it, so a
    /// collection built only at parameter time would still be describing the clean field.</summary>
    protected Dictionary<string, object> InputAttributes
    {
        get
        {
            SetInputAttributes();

            return _inputAttributes;
        }
    }

    Dictionary<string, object> _inputAttributes = Empty;

    static readonly Dictionary<string, object> Empty = [];

    string? _attributesFor;

    readonly string _describedBy = $"ns-field-{Guid.NewGuid():N}";

    /// <summary>The id naming the node that carries this field's refusal for anything that
    /// cannot see the screen — null while the field is clean, so the reference arrives and
    /// leaves with the message and never points at a node that is not rendered. It feeds both
    /// ends at once: NsFieldHelper stamps it on the node it renders, and the vendor input is
    /// handed the same string to describe itself by, so the two cannot be minted apart.
    ///
    /// Minted per field INSTANCE, never derived from the member it binds: a member name is
    /// shared by every form binding the same model, and two elements answering to one id make
    /// both descriptions unreachable.</summary>
    protected string? DescribedBy
    {
        get
        {
            return HasError() ? _describedBy : null;
        }
    }

    /// <summary>Whether this field selects its whole content the moment it receives focus —
    /// the mostrador feature (Leonardo, 2026-08-07: tab into Importe showing 0,00, type 500,
    /// it replaces — no manual clear first). Off by default: a field usually edited in place
    /// rather than replaced wholesale (email, phone, a multi-line note, a lookup's search
    /// box) must not opt in just by inheriting this base. Text, numeric and money fields
    /// override it; wired here once so no field hand-rolls its own onfocus.</summary>
    protected virtual bool SelectOnFocus => false;

    void SetInputAttributes()
    {
        var autocomplete = Autocomplete is { Length: > 0 } hint ? hint : null;
        var name = GetAccessibleName();
        var selectOnFocus = SelectOnFocus;
        var describedBy = DescribedBy;

        // A separator neither an autofill token nor a label can contain, so no two different
        // sets share a signature and a change is never mistaken for none.
        var signature = $"{autocomplete}\n{name}\n{selectOnFocus}\n{describedBy}";

        if (_attributesFor == signature)
        {
            return;
        }

        _attributesFor = signature;

        if (autocomplete is null && name is null && !selectOnFocus && describedBy is null)
        {
            _inputAttributes = Empty;
            return;
        }

        var attributes = new Dictionary<string, object>();

        if (autocomplete is not null)
        {
            attributes["autocomplete"] = autocomplete;
        }

        if (name is not null)
        {
            attributes["aria-label"] = name;
        }

        if (describedBy is not null)
        {
            attributes["aria-describedby"] = describedBy;
        }

        // A plain HTML attribute (no @ prefix), so Blazor never mediates it as a synthetic
        // event: the browser applies it as inline JS the instant the input gets focus, no
        // interop round trip. select() is native to every text-shaped <input>.
        if (selectOnFocus)
        {
            attributes["onfocus"] = "this.select()";
        }

        _inputAttributes = attributes;
    }

    /// <summary>The name of the table cell this field renders inside, already localized from
    /// that column's own member (NsTd). Only a field with a hidden label and no binding
    /// expression to derive from has any use for it.</summary>
    [CascadingParameter(Name = "CellLabel")]
    string? CellLabel { get; set; }

    [CascadingParameter(Name = "ParentDisabled")]
    bool ParentDisabled { get; set; }

    [CascadingParameter(Name = "ParentReadOnly")]
    bool ParentReadOnly { get; set; }

    protected bool IsDisabled => Disabled || ParentDisabled;

    protected bool IsReadOnly => ReadOnly || ParentReadOnly;

    /// <summary>Whether the field carries an answer at all — a binding left at its default
    /// (null, Guid.Empty, 0) is a question nobody answered. It is what lets a field whose
    /// displayed text is RESOLVED — a lookup naming its value out of a list that arrives from
    /// a read — float its label on the value itself, one render before it can name it.</summary>
    protected bool HoldsValue
    {
        get { return !EqualityComparer<TValue?>.Default.Equals(Value, default); }
    }

    protected override void OnParametersSet()
    {
        _for = For ?? ValueExpression;

        _fieldIdentifier = _for is null
            ? null
            : FieldIdentifier.Create(_for);

        _label = IsLabelHidden ? null : Label ?? BuildDefaultLabel();

        _declared = DeclaresRequired();

        Retrack();

        if (!ReferenceEquals(_subscribedEditContext, EditContext))
        {
            UnsubscribeFromEditContext();
            SubscribeToEditContext();
        }

        // The answer arriving is what lifts the refusal the last request raised. This is the
        // earliest place that can see it: SetValue awaits ValueChanged while the Value parameter
        // still holds the old one, so the new value is only here, on the parameter set the
        // binding's write triggers — which is also the only seam that catches a value a SCREEN
        // wrote into the model behind the field (CreateVoucherPage's party satellite filling the
        // fiscal condition), since that one travels no SetValue at all.
        //
        // Only once the form has asked: OwnProblem() answers for any empty required field, a
        // form nobody submitted included, and a refusal is drawn at Save, never at the keystroke
        // (intentional-ui.md, Refusal placement). NotifyOwnProblemChanged spends only the change,
        // so the fields the pick did not touch post nothing.
        if (_asked)
        {
            NotifyOwnProblemChanged();
        }
    }

    public virtual void Dispose()
    {
        DisposeCore();
    }

    protected virtual void DisposeCore()
    {
        Untrack();
        UnsubscribeFromEditContext();
    }

    // Announced on every parameter set, but only the change is spent: rebinding to a new model
    // instance leaves the previous identifier registered otherwise, and a stale registration is
    // a field the form believes is on screen when it is not.
    void Retrack()
    {
        if (ReferenceEquals(_tracker, FieldTracker) && Nullable.Equals(_tracked, _fieldIdentifier))
        {
            return;
        }

        Untrack();

        if (FieldTracker is { } tracker && _fieldIdentifier is { } identifier)
        {
            tracker.Track(identifier);
            _tracker = tracker;
            _tracked = identifier;
        }
    }

    void Untrack()
    {
        if (_tracker is { } tracker && _tracked is { } identifier)
        {
            tracker.Untrack(identifier);
        }

        _tracker = null;
        _tracked = null;
    }

    // The mark reads the declaration the form's own validator reads, so a field is marked
    // exactly when leaving it empty would refuse the submit. NsDataAnnotationsValidator
    // validates the EditContext's model and whatever a member opened to it ([Validated]) — the
    // same scope MessageValidator walks on the wire — so a [Required] on a row model (a sale
    // line, a channel) nothing opened is enforced by neither end and must not be drawn as
    // though it were.
    bool DeclaresRequired()
    {
        if (EditContext is null || _fieldIdentifier is not { } identifier)
        {
            return false;
        }

        if (!ValidatedModels.Covers(EditContext.Model, identifier.Model))
        {
            return false;
        }

        return RequiredMembers.Declares(identifier.Model.GetType(), identifier.FieldName);
    }

    /// <summary>What the bound member declares, for a field that draws a bound rather than a
    /// mark — the calendar's own last day, taken from the annotation that will refuse a later
    /// one. Scoped exactly like the required mark, to the models the form's own validator
    /// answers for: the EditContext's model and whatever a member opened to it
    /// (<c>[Validated]</c>), which is what MessageValidator walks on the wire, so a bound
    /// derived here is the one that refuses and cannot drift from it.</summary>
    protected TAttribute? Declared<TAttribute>() where TAttribute : Attribute
    {
        if (EditContext is null || _fieldIdentifier is not { } identifier)
        {
            return null;
        }

        if (!ValidatedModels.Covers(EditContext.Model, identifier.Model))
        {
            return null;
        }

        return DeclaredMembers.On<TAttribute>(identifier.Model.GetType(), identifier.FieldName);
    }

    // The vendor pickers (MudDatePicker, MudTimePicker — NsDateField, NsTimeField,
    // NsDateTimeField) raise their *Changed callback when a value is pushed INTO them from
    // the model, not only when someone picks one. A page whose read lands after the first
    // render and fills the model it already handed the form — the settings screens, where
    // the instance never changes so NsForm has no reason to rebuild its EditContext — then
    // got a field notification nobody caused, and the form reported unsaved changes on
    // arrival with the guard firing on the way out. An echo carries the value the field is
    // ALREADY displaying; a user edit carries a different one, and that is the whole
    // difference between them. ValueChanged still runs either way, so two-way binding is
    // untouched: only the change notification is withheld.
    protected async Task SetValue(TValue? value)
    {
        var edited = !EqualityComparer<TValue?>.Default.Equals(Value, value);

        await ValueChanged.InvokeAsync(value);

        if (edited)
        {
            NotifyEdited();
        }
    }

    protected string? GetLabel()
    {
        return _label;
    }

    // A blank Label is a distinct answer from an unset one, which derives its text: it asks
    // for no VISIBLE label, which is what an inline row editor wants — the column header above
    // the cell already says the word, and repeating it in every cell is noise.
    bool IsLabelHidden => Label is { } given && given.Trim().Length == 0;

    /// <summary>What the input is called for anything that cannot see the layout — a screen
    /// reader, and the semantic selector an e2e test reaches with. Null while a visible label
    /// renders, because that label already names the input through the vendor's own
    /// label/for association; hiding the label is what makes it needed, and the text is the
    /// one that would have been shown. A row editor's cell therefore answers with its column
    /// header: NsTh's For and the field's own binding resolve to the same member, so both
    /// sides derive the same localization key without ever naming each other. A field bound by
    /// hand (Value plus ValueChanged, as a lookup whose value is a row rather than the member
    /// must be) has no expression to inspect, and answers with the cell's own derived name —
    /// the same header, reached the other way round.</summary>
    protected string? GetAccessibleName()
    {
        if (!IsLabelHidden)
        {
            return null;
        }

        var derived = BuildDefaultLabel() is { Length: > 0 } name ? name : CellLabel;

        return derived is { Length: > 0 } ? derived : null;
    }

    protected virtual string? BuildDefaultLabel()
    {
        if (ForExpression.GetTarget(_for) is { } target)
        {
            var key = Metadata.KeyFor(target.Type, target.Member);

            return Strings.TryTranslate(key, out var text) ? text : FallbackLabel(key);
        }

        return ForExpression.GetMemberName(_for);
    }

    /// <summary>Label when the expression-derived key has no translation. Item-bearing
    /// fields (select, autocomplete) override this with the item type's concept key
    /// ("Directory.OrganizationRef" → "Organization"); the base keeps the untranslated
    /// key visible, never guessed.</summary>
    protected virtual string? FallbackLabel(string key)
    {
        return key;
    }

    protected virtual string? GetErrorText()
    {
        if (EditContext is null || _fieldIdentifier is null)
            return null;

        return EditContext.GetValidationMessages(_fieldIdentifier.Value).FirstOrDefault();
    }

    protected bool HasError()
    {
        return !string.IsNullOrWhiteSpace(GetErrorText());
    }

    void SubscribeToEditContext()
    {
        if (EditContext is null)
            return;

        EditContext.OnValidationStateChanged += HandleValidationStateChanged;
        EditContext.OnValidationRequested += HandleValidationRequested;
        _ownMessages = new ValidationMessageStore(EditContext);
        _subscribedEditContext = EditContext;
    }

    void UnsubscribeFromEditContext()
    {
        if (_subscribedEditContext is null)
            return;

        _subscribedEditContext.OnValidationStateChanged -= HandleValidationStateChanged;
        _subscribedEditContext.OnValidationRequested -= HandleValidationRequested;
        _ownMessages = null;
        // Forgotten with the store it was posted to: a field rebound to a new form starts owing
        // that form its problem, and a remembered one would make the first post look like a repeat.
        _posted = null;
        // The new form has asked nothing yet, so the field owes it no refusal until it does —
        // a create form opened after a refused one is as clean as the first.
        _asked = false;
        _subscribedEditContext = null;
    }

    void HandleValidationStateChanged(object? sender, ValidationStateChangedEventArgs e)
    {
        _ = InvokeAsync(StateHasChanged);
    }

    void HandleValidationRequested(object? sender, ValidationRequestedEventArgs e)
    {
        _asked = true;

        PostOwnProblems();
    }

    /// <summary>What the FIELD itself cannot accept, as opposed to what the bound member
    /// declares — the one case being text a date box could not read as a date. Null while the
    /// field has nothing of its own to say, which is every field that does not override this.
    ///
    /// It goes to the EditContext rather than staying a drawn message, because a problem the
    /// person can see and the submit cannot is how a value nobody entered reaches the server
    /// (nsail#1196): the box refuses the text out loud while the model keeps whatever it held,
    /// and Guardar would save that other value as though it were the one on screen.</summary>
    protected virtual string? GetOwnProblem()
    {
        return null;
    }

    /// <summary>Tells the form this field's own problem was raised or lifted between validation
    /// requests — typing is when a date box learns it cannot read its text, and a form that only
    /// validates on submit would otherwise let a refusal raised after the last request through,
    /// or keep drawing one it had already cleared.</summary>
    protected void NotifyOwnProblemChanged()
    {
        // Only the change is spent: a validation-state notification re-renders every field and
        // every tab of the form, and a date box left clean raises its text event all the same.
        if (OwnProblem() != _posted)
        {
            PostOwnProblems();
        }
    }

    // The refusal speaks first for the reason the date fields' own GetErrorText gives: what the
    // box visibly holds answers before what the model would have said about it.
    //
    // Required is otherwise cosmetic — the vendor's asterisk — and a Guid PolicyField whose
    // "unset" sentinel is Guid.Empty has no DataAnnotationsValidator path at all: RequiredAttribute
    // only rejects null, and Guid.Empty boxes to a non-null value (confirmed against the BCL, not
    // assumed). Owning the check here, keyed off the same Required flag every field already
    // carries, catches every TValue's sentinel uniformly and needs no per-field attribute.
    string? OwnProblem()
    {
        if (GetOwnProblem() is { } problem)
        {
            return problem;
        }

        if (Required && IsValueEmpty())
        {
            return Strings.Translate("Problems.Required");
        }

        return null;
    }

    // Both problems travel as one message because one store carries them and a Clear takes the
    // identifier's whole slot with it.
    void PostOwnProblems()
    {
        if (_ownMessages is null || _fieldIdentifier is not { } identifier)
        {
            return;
        }

        _posted = OwnProblem();

        _ownMessages.Clear(identifier);

        if (_posted is not null)
        {
            _ownMessages.Add(identifier, _posted);
        }

        _subscribedEditContext?.NotifyValidationStateChanged();
    }

    /// <summary>Whether Required has anything to complain about. A field holding a collection
    /// (NsMultiFileUpload) answers for its count, since an empty list is not the default
    /// reference the comparer below would call empty.</summary>
    protected virtual bool IsValueEmpty()
    {
        return IsEmpty(Value);
    }

    static bool IsEmpty(TValue? value)
    {
        if (value is string text)
        {
            return string.IsNullOrWhiteSpace(text);
        }

        return EqualityComparer<TValue>.Default.Equals(value!, default!);
    }

    /// <summary>Tells the form this field's value was edited by the user — every change
    /// travels through SetValue, including the ones a list field makes (an item added, an
    /// item cleared, the order changed), because the new list IS the new value.</summary>
    void NotifyEdited()
    {
        if (EditContext is not null && _fieldIdentifier is not null)
        {
            EditContext.NotifyFieldChanged(_fieldIdentifier.Value);
        }
    }

}