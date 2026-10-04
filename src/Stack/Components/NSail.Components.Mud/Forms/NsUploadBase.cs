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

    /// <summary>The refusal this field drew itself (a file over the limit), which outranks the
    /// validation message: it names what the user just did.</summary>
    string? _error;

    /// <summary>Shows what was uploaded, not only that something was. With a preview the
    /// component gains a second surface, and that is where the clear lives (top-right, on
    /// the image) — which leaves the field line free to keep the upload trigger, so picking
    /// again replaces in place instead of asking for a clear first.</summary>
    [Parameter]
    public bool Preview { get; set; }

    /// <summary>The file types the picker offers, in the browser's own accept vocabulary
    /// ("image/*", ".pdf"). Null offers everything.</summary>
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
        return _error ?? GetErrorText();
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

    /// <summary>Saves a picked file and answers the id it was filed under, or null when this
    /// field refused it — in which case the refusal is already drawn on the field.</summary>
    protected async Task<Guid?> Upload(IBrowserFile file)
    {
        _error = null;

        if (_store is not { } store)
        {
            return null;
        }

        if (file.Size > MaxSize)
        {
            _error = string.Format(Strings.Translate("Common.FileTooLarge"), MaxSize / (1024 * 1024));

            return null;
        }

        using var stream = file.OpenReadStream(MaxSize);
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer);

        return await store.Save(new FileContent(file.Name, file.ContentType, buffer.ToArray()));
    }
}
