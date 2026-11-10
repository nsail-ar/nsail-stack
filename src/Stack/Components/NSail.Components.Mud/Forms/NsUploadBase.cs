// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace NSail.Components;

/// <summary>What both upload fields share: where the bytes go, what is refused and the words
/// the house draws around a file. The PRESENTATION is each component's own — holding one file
/// and holding an ordered set are different shapes on screen, which is why they are two
/// components and not one with a mode.</summary>
public abstract class NsUploadBase<TValue> : NsFieldBase<TValue>
{
    [Inject]
    IServiceProvider Services { get; init; } = default!;

    IFileStore? _store;

    /// <summary>The refusals this field drew itself (a file over a limit, a type not accepted),
    /// which outrank the validation message: it names what the user just did.</summary>
    readonly List<string> _errors = [];

    /// <summary>Shows what was uploaded, not only that something was. With a preview the
    /// component gains a second surface, and that is where the clear lives (top-right, on
    /// the image) — which leaves the field line free to keep the upload trigger, so picking
    /// again replaces in place instead of asking for a clear first.</summary>
    [Parameter]
    public bool Preview { get; set; }

    /// <summary>The file types this field admits, in the browser's accept vocabulary ("image/*",
    /// ".pdf"); the picker offers them and the field ENFORCES them: a file whose extension or
    /// leading bytes fall outside is refused, and the stored content type comes from the Stack's
    /// allow-list, never the client. An extension the list does not know is admitted only when
    /// named or fitted by a token, and stored as application/octet-stream. A wildcard never
    /// admits svg or html. Null admits everything and keeps the client's type as given.</summary>
    [Parameter]
    public string? Accept { get; set; }

    /// <summary>The biggest file this field accepts, in bytes. The default keeps a careless
    /// pick from filling a database with a phone camera's raw output; a field that genuinely
    /// carries bigger documents raises its own. A file over it is refused ON THE FIELD, with
    /// the limit in the sentence — never dropped silently by the vendor's own knob.</summary>
    [Parameter]
    public long MaxSize { get; set; } = 5 * 1024 * 1024;

    /// <summary>Where the bytes go. Defaults to the registered store, which files under the
    /// session's own organization; a page whose files belong to somebody else — a patient's
    /// study — hands its own, because that ownership is the page's knowledge.</summary>
    [Parameter]
    public IFileStore? Store { get; set; }

    protected override void OnParametersSet()
    {
        base.OnParametersSet();

        _store = Store ?? Services.GetService(typeof(IFileStore)) as IFileStore;
    }

    protected string? GetFieldError()
    {
        return _errors.Count > 0 ? string.Join(" · ", _errors) : GetErrorText();
    }

    protected bool IsTriggerDisabled()
    {
        return IsDisabled || IsReadOnly || _store is null;
    }

    protected bool IsClearDisabled()
    {
        return IsDisabled || IsReadOnly;
    }

    protected string GetTriggerLabel()
    {
        return Strings.Translate("Common.Upload");
    }

    protected string GetClearLabel()
    {
        return Strings.Translate("Mud.MudInput_Clear");
    }

    protected string? GetUrl(Guid id)
    {
        return _store?.GetUrl(id);
    }

    // A stored id whose name this visit never learned reads as the generic word, never as
    // its Guid: Assets serves bytes, not metadata, and an id shown to a person is a defect
    // of its own.
    protected string GetLineText(Guid? id)
    {
        if (id is not { } value)
        {
            return Strings.Translate("Common.NoFile");
        }

        return _store?.GetName(value) ?? Strings.Translate("Common.File");
    }

    /// <summary>Forgets the refusals of the previous selection; one selection calls it once,
    /// before its files, so every refusal of that selection stays on the field.</summary>
    protected void ClearRefusals()
    {
        _errors.Clear();
    }

    protected void RefuseTotal(IBrowserFile file, long limit)
    {
        _errors.Add($"{file.Name}: " + string.Format(Strings.Translate("Common.FilesTooLarge"), UploadPolicy.FormatSize(limit)));
    }

    /// <summary>Saves a picked file and answers the id it was filed under, or null when this
    /// field refused it — in which case the refusal is already drawn on the field.</summary>
    protected async Task<Guid?> Upload(IBrowserFile file)
    {
        if (_store is not { } store)
        {
            return null;
        }

        if (file.Size > MaxSize)
        {
            _errors.Add($"{file.Name}: " + string.Format(Strings.Translate("Common.FileTooLarge"), UploadPolicy.FormatSize(MaxSize)));

            return null;
        }

        var content = new byte[file.Size];

        await using (var stream = file.OpenReadStream(MaxSize))
        {
            await stream.ReadExactlyAsync(content);
        }

        var contentType = UploadPolicy.Resolve(Accept, file.Name, file.ContentType, content[..Math.Min(content.Length, UploadPolicy.HeadLength)]);

        if (contentType is null)
        {
            _errors.Add($"{file.Name}: " + Strings.Translate("Common.FileTypeNotAccepted"));

            return null;
        }

        return await store.Save(new FileContent(file.Name, contentType, content));
    }
}
