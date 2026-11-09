// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Text;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSail.Localization;
using NSail.Metadata;

namespace NSail.Components.Tests.Fixtures;

/// <summary>The host both upload fields are rendered in: the same services, the same strings
/// and the same recording store, so the single and the multi component are measured against
/// one another rather than against two different rigs.</summary>
public abstract class UploadBunitContext : BunitContext, IAsyncLifetime
{
    // MudBlazor services here are IAsyncDisposable-only, which bUnit's synchronous teardown
    // cannot dispose — the NsActionColumnTests rule.
    Task IAsyncLifetime.InitializeAsync()
    {
        return Task.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
    }

    protected RecordingFileStore FileStore { get; } = new();

    protected UploadBunitContext()
    {
        // No MudPopoverProvider in this host: the vendor otherwise refuses to build the popover
        // a tooltip or a picker owns, and none of these assertions is about a popover.
        Services.AddMudServices(config => config.PopoverOptions.CheckForPopoverProvider = false);
        Services.AddScoped<MudBlazor.IKeyInterceptorService, NoopKeyInterceptorService>();
        Services.AddSingleton(new StringCatalog([new FixedStrings(new Dictionary<string, string>
        {
            ["Common.Upload"] = "Subir",
            ["Common.NoFile"] = "Ningún archivo elegido",
            ["Common.File"] = "Archivo",
            ["Common.FileTooLarge"] = "El archivo supera los {0}",
            ["Common.FileTypeNotAccepted"] = "Tipo no aceptado",
            ["Common.FilesTooLarge"] = "Selección supera los {0}",
            ["Mud.MudInput_Clear"] = "Limpiar"
        })]));
        Services.AddSingleton<LanguageProvider>();
        Services.AddSingleton<MetadataProvider>();
        Services.AddSingleton<StringManager>();
        Services.AddSingleton<IFileStore>(FileStore);
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    protected static void Pick(IRenderedComponent<IComponent> cut, string name, string? content = null)
    {
        var bytes = content is not null
            ? Encoding.UTF8.GetBytes(content)
            : name.EndsWith(".pdf") ? "%PDF-1.4"u8.ToArray()
            : name.EndsWith(".png") ? [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]
            : "<svg/>"u8.ToArray();

        PickBytes(cut, name, bytes);
    }

    protected static void PickBytes(IRenderedComponent<IComponent> cut, string name, byte[] bytes)
    {
        cut.FindComponent<InputFile>()
            .UploadFiles(InputFileContent.CreateFromBinary(bytes, name, contentType: "image/svg+xml"));
    }

    protected static void PickMany(IRenderedComponent<IComponent> cut, params (string Name, byte[] Bytes)[] files)
    {
        cut.FindComponent<InputFile>()
            .UploadFiles([.. files.Select(f => InputFileContent.CreateFromBinary(f.Bytes, f.Name, contentType: "image/svg+xml"))]);
    }
}
