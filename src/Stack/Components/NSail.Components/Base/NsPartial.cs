// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Icons;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using NSail.Localization;
using NSail.Messaging;
using NSail.Messaging.Runtime;
using NSail.Messaging.Runtime.Context;
using NSail.Messaging.Runtime.Publishing;
using NSail.Metadata;
using NSail.Problems;
using NSail.Security;

namespace NSail.Components;

/// <summary>Base for kit components — a piece of a page (a list, an editor) that talks to
/// the application: messaging, localization, dialogs and typed URLs. Pages derive from it
/// through NsPage; widgets that only render parameters stay on NsComponent.</summary>
public abstract class NsPartial : NsComponent
{
    Dictionary<string, Action>? _handed;
    PersistentComponentState? _state;
    bool _looked;

    [Inject]
    protected Mediator Mediator { get; init; } = default!;

    [Inject]
    protected DialogManager Dialogs { get; init; } = default!;

    [Inject]
    protected StringManager Strings { get; init; } = default!;

    [Inject]
    protected MetadataProvider Metadata { get; init; } = default!;

    [Inject]
    protected RouteTable Routes { get; init; } = default!;

    [Inject]
    protected IServiceProvider Services { get; init; } = default!;

    /// <summary>Whether the session could send this message at all — the message twin of the
    /// page gate, for a control whose permission is a message rather than a destination page
    /// (a row's delete, a card's own acts). Optimistic like every other client-side check:
    /// field constraints are not read here, so it answers no only when no policy covers the
    /// key, and the server's gate stays the one that decides.
    ///
    /// Synchronous, which is why it reads SecurityManager instead of IAuthorizationService:
    /// a control hidden on the first render and shown on the second is the content-to-content
    /// flash the one-arrival rule forbids, and markup like this renders dozens of them.
    ///
    /// Unconstrained, exactly like the [Authorize&lt;TMessage&gt;] it mirrors: a command and a
    /// query implement two unrelated interfaces, so one type parameter cannot name both.</summary>
    protected bool CanSend<TMessage>()
    {
        // Resolved, never injected: a host with no security registered (a component test, a
        // surface without Iam) must still render its markup rather than fail to construct it
        // — the permissive default the endpoint gate and the relation provider already take.
        var security = Services.GetService(typeof(SecurityManager)) as SecurityManager;
        var sessions = Services.GetService(typeof(SessionProvider)) as SessionProvider;

        return security is null
            || sessions is null
            || security.PreAuthorize(typeof(TMessage), sessions.Session);
    }

    /// <summary>Whether the session could send this message with this flag field set this
    /// way — the field twin of the check above, for a bool a policy may pin
    /// ([PolicyField(RestrictAs.Flag)], permissions.md). A flag is a literal the browser can
    /// decide on its own, so a control the caller may not tick is not drawn at all rather
    /// than drawn and refused on submit; the server's gate stays the one that decides.</summary>
    protected bool CanSend<TMessage>(string field, bool value)
    {
        var security = Services.GetService(typeof(SecurityManager)) as SecurityManager;
        var sessions = Services.GetService(typeof(SessionProvider)) as SessionProvider;

        return security is null
            || sessions is null
            || security.Permits(typeof(TMessage), field, value, sessions.Session);
    }

    /// <summary>Resolves a string trying "{Area}.{ComponentName}.{key}" first, then the key
    /// as-is, then the fallback (or the key itself).</summary>
    protected string Translate(string key, string? fallback = null)
    {
        return Strings.TryTranslate(Metadata.KeyFor(GetType(), key), out var scoped)
            ? scoped
            : Strings.Translate(key, fallback);
    }

    /// <summary>Count-aware overload of Translate: resolves the same way, then picks the
    /// ".Plural" variant for any count other than one.</summary>
    protected string Translate(string key, int count)
    {
        var scoped = Metadata.KeyFor(GetType(), key);

        if (Strings.TryTranslate(scoped, out _) || Strings.TryTranslate($"{scoped}.Plural", out _))
        {
            return Strings.Translate(scoped, count);
        }

        return Strings.Translate(key, count);
    }

    /// <summary>Localized text for an enum value ("{Area}.{Enum}.{Value}"), the value's
    /// name as fallback.</summary>
    protected string Translate(Enum? value)
    {
        if (value is null)
        {
            return string.Empty;
        }

        return Strings.Translate(value);
    }

    /// <summary>The enum's values as select options with their localized texts, plus a
    /// leading null labeled "Common.All" — filter selects. Pass all: false for forms.</summary>
    protected IEnumerable<Option<TEnum?>> GetItems<TEnum>(bool all = true) where TEnum : struct, Enum
    {
        var values = Enum.GetValues<TEnum>()
            .Select(value => new Option<TEnum?>(value, Strings.Translate(value)));

        return all
            ? [new Option<TEnum?>(null, Strings.Translate("Common.All")), .. values]
            : values;
    }

    protected Task<bool> Confirm(string message, string? title = null)
    {
        return Dialogs.Confirm(message, title);
    }

    protected Task Alert(string message, string? title = null)
    {
        return Dialogs.Alert(message, title);
    }

    protected void Notify(string message, NsSeverity severity = NsSeverity.Info)
    {
        Dialogs.Notify(message, severity);
    }

    /// <summary>Success toast for a message that was just sent — the mirror of ConfirmSend:
    /// that confirms before sending with the message's own text ("{Area}.{Message}.ConfirmMessage"),
    /// this notifies after with ("{Area}.{Message}.SentMessage"), "Common.Saved" otherwise
    /// (right for the Create/Update messages that make up the current callers; a Delete
    /// message wants its own SentMessage key since "Saved" would be wrong for it).</summary>
    protected void NotifySent<TMessage>() where TMessage : IMessage
    {
        NotifySent(typeof(TMessage));
    }

    /// <summary>The same toast for a message the constraint above cannot name: IMessage and
    /// IMessage&lt;TResult&gt; share no base, so a message that answers something has no
    /// generic overload to reach — and whether a send returns a value has nothing to do with
    /// whether it is worth announcing.</summary>
    protected void NotifySent(Type messageType)
    {
        Notify(GetSentText(messageType), NsSeverity.Success);
    }

    /// <summary>The sentence NotifySent announces, without announcing it — for a surface that
    /// says what a send did somewhere other than a toast.</summary>
    protected string GetSentText(Type messageType)
    {
        return Strings.TryTranslate(Metadata.KeyFor(messageType, "SentMessage"), out var scoped)
            ? scoped
            : Strings.Translate("Common.Saved");
    }

    /// <summary>Asks for confirmation with the message type's own text
    /// ("{Area}.{Message}.ConfirmMessage", "Common.ConfirmSend" when absent) and sends it
    /// when accepted; returns whether it was sent.
    ///
    /// A send the server refuses with the InUse code answers with the message type's own text
    /// too ("{Area}.{Message}.InUseMessage", "Common.InUse" when absent) — the same derivation
    /// NotifySent and the confirmation itself use, so the handler names a code and the client
    /// owns every word. It is a dialog and not a toast on purpose: a refusal that fades is
    /// worse than a crash (principles.md, "silence is the worst failure"), and this is the one
    /// refusal with no field to anchor under.</summary>
    protected async Task<bool> ConfirmSend(IMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        var key = Metadata.KeyFor(message.GetType(), "ConfirmMessage");

        var text = Strings.TryTranslate(key, out var scoped)
            ? scoped
            : Strings.Translate("Common.ConfirmSend");

        if (!await Confirm(text))
        {
            return false;
        }

        try
        {
            await Send(message);
        }
        catch (BusinessException refusal) when (refusal.Code == BusinessProblem.InUseCode)
        {
            await Alert(GetInUseText(message.GetType()));

            return false;
        }

        return true;
    }

    string GetInUseText(Type message)
    {
        return Strings.TryTranslate(Metadata.KeyFor(message, "InUseMessage"), out var scoped)
            ? scoped
            : Strings.Translate("Common.InUse");
    }

    /// <summary>Opens a component in a modal dialog (see DialogManager.Open).</summary>
    protected Task OpenDialog<TComponent>(
        string? title = null,
        IReadOnlyDictionary<string, object?>? parameters = null)
        where TComponent : IComponent
    {
        return Dialogs.Open<TComponent>(title, parameters);
    }

    /// <summary>Typed page URL from the route table; with multiple templates the one
    /// whose tokens the parameters satisfy wins.</summary>
    protected string GetUrl<TPage>(object? parameters = null) where TPage : IComponent
    {
        return Routes.GetUrl<TPage>(parameters);
    }

    /// <summary>Sends a message and returns its result. The component's own CancellationToken
    /// is used unless one is passed (an event's own token — search, load, submit).</summary>
    protected Task<TResult> Send<TResult>(IMessage<TResult> message, CancellationToken? cancellationToken = null)
    {
        return Mediator.Send(message, cancellationToken ?? CancellationToken);
    }

    /// <summary>Sends a message with no result (see the result overload for the token rule).</summary>
    protected Task Send(IMessage message, CancellationToken? cancellationToken = null)
    {
        return Mediator.Send(message, cancellationToken ?? CancellationToken);
    }

    /// <summary>The control whose act this component is carrying out — the token a lookup put
    /// in the create href it opened this page with. Every event published from here wears it,
    /// which is how the lookup that asked recognizes its own save and no other lookup adopts
    /// it. Null on a component nobody sent here, and a save with no token is adopted by
    /// nobody.</summary>
    protected virtual string? Source
    {
        get { return null; }
    }

    /// <summary>Publishes a UI event to every subscriber (see Send for the token rule).</summary>
    protected Task Publish(IMessage message, CancellationToken? cancellationToken = null)
    {
        return Publish(message, null, cancellationToken);
    }

    /// <summary>Publish carrying headers, with this component's own Source added to them — a
    /// create page names nobody and the correlation rides along anyway, so no publisher writes
    /// the token and none can forget it. An explicitly passed source wins: the caller knows
    /// something this base cannot.</summary>
    protected Task Publish(
        IMessage message, IReadOnlyDictionary<string, string>? headers, CancellationToken? cancellationToken = null)
    {
        return Mediator.Publish(message, WithSource(headers), cancellationToken ?? CancellationToken);
    }

    IReadOnlyDictionary<string, string>? WithSource(IReadOnlyDictionary<string, string>? headers)
    {
        if (Source is not { } source)
        {
            return headers;
        }

        if (headers is not null && headers.ContainsKey(MessageHeaders.Source))
        {
            return headers;
        }

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { [MessageHeaders.Source] = source };

        if (headers is not null)
        {
            foreach (var (name, value) in headers)
            {
                values[name] = value;
            }
        }

        return values;
    }

    /// <summary>Send for a read that a prerender already resolved: the prerendering server
    /// sends it and persists the answer, and the WebAssembly client adopts that answer instead
    /// of asking for it again — one read per page load rather than two, and no default state
    /// rendered between them (intentional-ui.md, one arrival).
    ///
    /// Opt-in, and it has to be: over a hundred components inherit this base and most of them
    /// read nothing a prerender could hand over. Choosing this over Send IS the declaration —
    /// a component that keeps calling Send behaves exactly as it did.
    ///
    /// The adoption is SYNCHRONOUS, which is the whole property: the returned task is already
    /// complete when an answer was handed over, so OnParametersSetAsync never yields and the
    /// client's first render is the prerender's last one. An await here costs precisely the
    /// render this exists to save.
    ///
    /// The key is derived rather than written — the component's own type names the card (both
    /// dashboards already key their cells by it) and the message type names the read, so a card
    /// with two reads hands both over. Scope is what tells two instances of one card type apart
    /// on the same page: a subject card passes its subject.</summary>
    protected async Task<TResult> Handoff<TResult>(
        IMessage<TResult> message, object? scope = null, CancellationToken? cancellationToken = null)
    {
        ArgumentNullException.ThrowIfNull(message);

        var key = GetHandoffKey(message.GetType(), scope);

        if (GetHandoffState() is { } state && state.TryTakeFromJson<TResult>(key, out var handed) && handed is not null)
        {
            return handed;
        }

        var result = await Send(message, cancellationToken);

        Remember(key, result);

        return result;
    }

    string GetHandoffKey(Type message, object? scope)
    {
        var key = $"{GetType().Name}.{message.Name}";

        return scope is null ? key : $"{key}:{scope}";
    }

    // Resolved, never injected, for CanSend's reason: a host without the handoff registered —
    // a component test, a surface that is not a Blazor Web App — must still render.
    PersistentComponentState? GetHandoffState()
    {
        if (!_looked)
        {
            _looked = true;
            _state = Services.GetService(typeof(PersistentComponentState)) as PersistentComponentState;
        }

        return _state;
    }

    void Remember<TResult>(string key, TResult result)
    {
        if (GetHandoffState() is not { } state)
        {
            return;
        }

        // The render mode is named rather than left to .NET, even though this callback belongs
        // to a component and the tree could in principle be read for it: the reading happens at
        // the END of the prerender, and a component that read on the way to a screen it does not
        // reach is gone by then. AuthorizeRouteView draws its Authorizing placeholder inside the
        // DEFAULT layout, so every page's arrival mounts that layout's cards for as long as the
        // session takes to resolve, and a page whose own layout is a different one drops them
        // when the answer lands. Asked for the render mode of a component the renderer no longer
        // holds, the framework dereferences a null state and the whole document answers 500 —
        // a card's optimisation taking the screen with it. WebAssembly and only WebAssembly for
        // DeviceProvider's reason: it is the one client that starts a second time in a second
        // process, with no request left to read.
        if (_handed is null)
        {
            _handed = [];

            Using<IDisposable>(state.RegisterOnPersisting(PersistHanded, RenderMode.InteractiveWebAssembly));
        }

        // Assigned rather than added: a card that re-read before the prerender ended would
        // otherwise persist the same key twice, and the second one throws.
        _handed[key] = () => state.PersistAsJson(key, result);
    }

    Task PersistHanded()
    {
        foreach (var persist in _handed!.Values)
        {
            persist();
        }

        return Task.CompletedTask;
    }

    /// <summary>Subscribes to a published message for the lifetime of this component (marshalled
    /// to the renderer). InvokeAsync only dispatches the handler onto the render thread — it is
    /// not one of Blazor's own automatic-render points the way OnParametersSet or a bound event
    /// is, so a handler that only mutates a field left the change invisible until something else
    /// happened to redraw the component (stories.md 2026-08-07: the client ficha's own address
    /// card, the OT grid — both subscribed correctly and both went stale the moment 1bb12a22
    /// stopped tearing the tree down on every navigation and hiding it). Redrawing after every
    /// handler is this method's job precisely because Subscribe is what promised "stays current"
    /// to begin with; a handler that manages its own redraw (NsTable.Reload) just gets a second,
    /// harmless one.</summary>
    protected IDisposable Subscribe<TMessage>(SubscriptionDelegate<TMessage> handler)
        where TMessage : IMessage
    {
        ArgumentNullException.ThrowIfNull(handler);

        return Using<IDisposable>(Mediator.Subscribe<TMessage>((message, cancellationToken) =>
            InvokeAsync(async () =>
            {
                await handler(message, cancellationToken);
                StateHasChanged();
            })));
    }

    /// <summary>This component's own title ("{Area}.{ComponentName}.Title") — render with
    /// &lt;NsText As="Title"&gt;@GetTitle()&lt;/NsText&gt;.</summary>
    protected string GetTitle()
    {
        return Strings.Translate(Metadata.KeyFor(GetType(), "Title"));
    }

    /// <summary>Binds a view method to an ActionItem for NsAction — pass the bare method
    /// group (GetAction(Delete, row, NsIcons.Delete)) so the captured name is clean; a
    /// lambda would capture the whole lambda text instead of just the method name. Name
    /// drives the label ("Actions.{Method}"), the same convention contributed ActionItems
    /// use. The handler runs through the Runner, so a rule the server rejects surfaces as a
    /// problem instead of vanishing.
    ///
    /// writes: false is the act that survives a form which refuses writes — it probes, it opens
    /// a conversation, it goes somewhere (ActionItem.Writes). Every deed leaves it alone.</summary>
    protected ActionItem GetAction(Action handler, Glyph? icon = null, bool writes = true, [CallerArgumentExpression(nameof(handler))] string name = "")
    {
        return new() { Name = name, Icon = icon, Writes = writes, OnClick = () => Run(() => { handler(); return Task.CompletedTask; }) };
    }

    protected ActionItem GetAction(Func<Task> handler, Glyph? icon = null, bool writes = true, [CallerArgumentExpression(nameof(handler))] string name = "")
    {
        return new() { Name = name, Icon = icon, Writes = writes, OnClick = () => Run(handler) };
    }

    protected ActionItem GetAction<TArg>(Action<TArg> handler, TArg arg, Glyph? icon = null, bool writes = true, [CallerArgumentExpression(nameof(handler))] string name = "")
    {
        return new() { Name = name, Icon = icon, Writes = writes, OnClick = () => Run(() => { handler(arg); return Task.CompletedTask; }) };
    }

    protected ActionItem GetAction<TArg>(Func<TArg, Task> handler, TArg arg, Glyph? icon = null, bool writes = true, [CallerArgumentExpression(nameof(handler))] string name = "")
    {
        return new() { Name = name, Icon = icon, Writes = writes, OnClick = () => Run(() => handler(arg)) };
    }

    /// <summary>The link twin of GetAction: an ActionItem that opens a page — what a grid row
    /// hands its one toolbar as a fixed verb (Leading) or as its constant. The URL comes from
    /// the route table at render and the label from the page's own title, so moving a row's
    /// NsPageLink in here writes neither of them. Weight is the counter-frequency order the
    /// toolbar's cap reads. Name is the escape hatch NsPageLink's Label is: pass one only
    /// when the row reaches that page for a meaning its title does not carry (correcting a
    /// note, not writing one), and the label then resolves as "Actions.{Name}". Group is the
    /// row's affinity, any enum the page owns: the toolbar rules its overflow menu between two
    /// consecutive items that do not share one. It writes nothing by construction, so it is the
    /// one act a form that refuses writes leaves standing (ActionItem.Writes).</summary>
    protected static ActionItem GetLink<TPage>(Glyph? icon = null, object? parameters = null, Surface? target = null, int weight = 0, string? name = null, Enum? group = null)
        where TPage : IComponent
    {
        return new()
        {
            Name = name ?? typeof(TPage).Name,
            Icon = icon,
            PageType = typeof(TPage),
            Parameters = parameters,
            Target = target,
            Weight = weight,
            Group = group,
            Writes = false,
        };
    }
}
