# UE Log Viewer

[![CI](https://github.com/miknios/ue-log-viewer/actions/workflows/ci.yml/badge.svg)](https://github.com/miknios/ue-log-viewer/actions/workflows/ci.yml)
[![Latest release](https://img.shields.io/github/v/release/miknios/ue-log-viewer)](https://github.com/miknios/ue-log-viewer/releases/latest)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

A fast, standalone viewer for Unreal Engine (UE4 and UE5) log files on Windows. Open any log, live or finished, and filter it by category, verbosity and text, with auto-scroll that keeps up while the engine writes.

![UE Log Viewer showing a live log with a few categories hidden](docs/screenshot.png)

## Why a separate app

- **Any log, not just the current session.** Open the live editor log, logs of previous runs (`*-backup-*.log`), standalone game and dedicated server logs, logs from a packaged build or someone else's machine, and build, cook and packaging logs from UnrealBuildTool and AutomationTool. None of those show up in the editor's Output Log.
- **No editor needed.** It reads the file the engine writes anyway, so it keeps working while the editor is closed, recompiling, hung or crashed. After a crash, the last lines before it are still on screen.
- **Survives restarts.** When the engine starts a new run and rotates the log, the view resets and keeps following the same file.
- **Filtering that stays out of the way.** Hide any number of categories and it still sticks to the newest line. The editor's Output Log stops auto-scrolling as soon as a category is hidden. (In the stock `SOutputLog.h`, `FLogFilter::IsFilterSet()` ignores the "show all categories" flag, so the scroll request targets a line past the end of the filtered list and is dropped.)
- **Lightweight and portable.** A single exe, nothing to install, settings kept in one file next to it. It doesn't need a project, plugin or engine change.

## Download

Get the latest build from the [Releases](https://github.com/miknios/ue-log-viewer/releases/latest) page. There are two zips:

| File | Size | Requirements |
|---|---|---|
| `UeLogViewer-<version>-win-x64.zip` | ~60 MB | None. Self-contained, runs on any 64-bit Windows 10 or 11. **Pick this one if unsure.** |
| `UeLogViewer-<version>-win-x64-framework-dependent.zip` | ~200 KB | [.NET 9 Desktop Runtime (x64)](https://dotnet.microsoft.com/download/dotnet/9.0) |

Unzip into any folder you can write to and run `UeLogViewer.exe`.

The exe is not code-signed, so Windows SmartScreen may show "Windows protected your PC" the first time you run it. Click **More info**, then **Run anyway**.

## Usage

```
UeLogViewer.exe [path\to\file.log]
```

Pick a log with **Browse…**, or paste its path into the box and press Enter. The last opened file is opened again on the next start. A path given on the command line takes precedence and becomes the remembered one. If the file doesn't exist yet, the viewer waits for it to appear.

### Where Unreal keeps its logs

| Log | Typical location |
|---|---|
| Editor, PIE, game and server runs from a project | `<Project>\Saved\Logs\<Project>.log` |
| Previous runs | `<Project>\Saved\Logs\<Project>-backup-<date>.log` |
| Packaged game | `%LOCALAPPDATA%\<Project>\Saved\Logs\` |
| UnrealBuildTool | `%LOCALAPPDATA%\UnrealBuildTool\Log.txt` (UE5), `Engine\Programs\UnrealBuildTool\Log.txt` (UE4) |
| AutomationTool (cook, package, BuildGraph) | `Engine\Programs\AutomationTool\Saved\Logs\` |

Lines without a `Category:` prefix, which are common in build logs, are listed under the **(none)** category. Search works on every line.

### Settings

The last path, category choices, level toggles, search and panel width are saved in `UeLogViewer.settings.json` **next to the exe**. Nothing is written to the registry, `%APPDATA%` or anywhere else. If the folder isn't writable the viewer still works but doesn't remember settings. Delete the file to reset everything.

## Features

- **Live tailing.** Polls the file every 200 ms and opens it with full sharing, so the engine can keep writing, renaming and deleting it. A new run is detected when the start of the file changes or the file shrinks.
- **Categories.** The list is built from what appears in the file, with line counts. Use the checkboxes, **All** / **None**, or the find box. Double-click a category (or right-click it) to show only that one. **Show new categories** decides whether categories that appear later start visible. Right-click a log line to show only, or hide, its category.
- **Verbosity.** Errors (including Fatal), Warnings and Logs (everything else) toggles, with counts.
- **Search.** Case-insensitive substring or regular expression. Esc clears it.
- **Follow end.** Sticks to the newest line, turns off when you scroll up, and turns back on when you reach the bottom (or press End, or tick it).
- **Multi-line messages.** Lines without a category prefix (callstacks, dumps) take the category and verbosity of the line before them, so filters keep whole messages together and colour them the same.
- **Timestamps.** Show or hide the `[time][frame]` prefix. Ctrl+C copies the selected lines in full, timestamps included.
- **Clear.** Hides everything logged so far without touching the file.

## Building from source

Requirements: Windows and the [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0). Open `UeLogViewer.sln` in Visual Studio or Rider, or use the command line:

```
dotnet build -c Release
dotnet test -c Release
```

Single-file builds, as published on the Releases page:

```
# Self-contained (no runtime needed)
dotnet publish src/UeLogViewer -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish/self-contained

# Framework-dependent (needs the .NET 9 Desktop Runtime)
dotnet publish src/UeLogViewer -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish/framework-dependent
```

Releases are built by GitHub Actions ([`release.yml`](.github/workflows/release.yml)) when a `v*` tag is pushed.

### Project layout

| Path | Contents |
|---|---|
| `src/UeLogViewer/LogParser.cs` | Parses `[time][frame]Category: Verbosity: message` lines and continuation lines |
| `src/UeLogViewer/LogTailer.cs` | Background polling, partial-line buffering, BOM handling, new-run detection |
| `src/UeLogViewer/LogEntry.cs` | Parsed line and verbosity level |
| `src/UeLogViewer/MainWindow.xaml(.cs)` | UI, filtering, follow logic |
| `src/UeLogViewer/CategoryItem.cs` | Category list row |
| `src/UeLogViewer/ViewerSettings.cs` | Portable settings file |
| `tests/UeLogViewer.Tests/` | Unit tests for the log line parser |

## Contributing

Bug reports, ideas and pull requests are welcome. For a bug, a few lines of the log that trigger it help a lot (remove anything private first).

## License

[MIT](LICENSE)

Unreal Engine is a trademark of Epic Games, Inc. This project is not affiliated with or endorsed by Epic Games.
