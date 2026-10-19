// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components;

/// <summary>The layout's companion region: whether it stands open, and the words whoever opened
/// it handed over. Scoped, injected by what fills the region and by whatever door opens it —
/// the region is part of the frame rather than a place, so it carries no route and this is the
/// only way to drive it.
///
/// <para>State alone: what is drawn is <c>NsMainLayout</c>'s Companion slot, and remembering the
/// answer per browser is that layout's too. A reader redraws on <see cref="Changed"/>.</para></summary>
public sealed class Companion
{
    public bool IsOpen { get; private set; }

    /// <summary>What the door handed over as it opened the region — the text the menu's no-match
    /// entry was typed with, for the content to start from. Null when it was opened with nothing,
    /// and cleared by a close: it is one door's handover, not a field of the region's.</summary>
    public string? Text { get; private set; }

    public event Action? Changed;

    public void Open()
    {
        Set(true, null);
    }

    /// <summary>Opens carrying text for the content to start from, and hands it over on a region
    /// already open too — a second ask from the same door is a new question, not a no-op.</summary>
    public void Open(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        Set(true, text);
    }

    public void Close()
    {
        Set(false, null);
    }

    public void Toggle()
    {
        Set(!IsOpen, null);
    }

    void Set(bool open, string? text)
    {
        // A handover raises even where the region already stood open: the content reads Text off
        // this event, and a second question would otherwise never reach it.
        if (IsOpen == open && text is null && Text is null)
        {
            return;
        }

        IsOpen = open;
        Text = text;

        Changed?.Invoke();
    }
}
