# Pagefold

Pagefold is a Windows comic reader for CBZ, CBR, CB7, CBT, and PDF libraries.

It is built with C#/.NET and WPF, with the reader logic split into a reusable core library so archive parsing, page ordering, metadata, reading state, bookmarks, and preloading can evolve separately from the desktop UI.

## Features

- CBZ, CBR, CB7, and CBT archive reading through SharpCompress
- PDF viewing through WebView2
- Natural comic page sorting
- ComicInfo.xml metadata reading
- Folder/library scanning
- Persistent library, recent files, bookmarks, settings, and last-read pages
- Single page, double page, and manga right-to-left navigation
- Fit/manual zoom, mouse-wheel zoom, and middle-mouse panning
- Page cache and background preload queue
- Embedded app icon and Windows release build output named `Pagefold.exe`
- GitHub Actions build and test workflow

## Project Layout

```text
Pagefold.App      WPF desktop app
Pagefold.Core     Reader, archive, metadata, cache, library services
Pagefold.Tests    Core behavior tests
```

## Build

```powershell
dotnet restore Pagefold.slnx
dotnet build Pagefold.slnx --configuration Release
dotnet test Pagefold.slnx --configuration Release
```

## Publish

Framework-dependent release:

```powershell
dotnet publish .\Pagefold.App\Pagefold.App.csproj -c Release -r win-x64 --self-contained false -o .\artifacts\Pagefold-win-x64
```

Self-contained release:

```powershell
dotnet publish .\Pagefold.App\Pagefold.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o .\artifacts\Pagefold-win-x64-self-contained
```

## State

Pagefold stores user state here:

```text
%AppData%\Pagefold\state.json
```

Build and publish outputs are ignored by git.

## Roadmap

- Library cover thumbnails
- Dedicated PDF page rendering pipeline
- Installer/MSIX packaging
- Keyboard shortcut customization
- Theme presets
