<div align="right">
  <a href="README.md"><img src="https://img.shields.io/badge/Language-English-blue?style=flat-square" alt="English"></a>
  <a href="README.ru.md"><img src="https://img.shields.io/badge/Язык-Русский-gray?style=flat-square" alt="Русский"></a>
</div>

# NotionDesk

<div align="center">

![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011-0078D6?style=flat-square&logo=windows)
![.NET 9](https://img.shields.io/badge/.NET-9.0%20WPF-512BD4?style=flat-square&logo=dotnet)
![RAM Footprint](https://img.shields.io/badge/RAM-~25--40%20MB-22C55E?style=flat-square)
![License](https://img.shields.io/badge/License-MIT-F59E0B?style=flat-square)

**Lightweight native desktop Markdown viewer with real-time file synchronization and CLI command extraction.**

[Features](#features) • [Installation](#installation) • [Controls](#controls) • [Build from Source](#build-from-source) • [Roadmap](ROADMAP.md)

</div>

---

## Overview

Most desktop note-taking and viewer tools rely on heavy web engines (Chromium/Electron), consuming 200–500 MB of RAM while idling on the desktop.

**NotionDesk** is built as a native C#/.NET 9 Windows application:
- **Low resource usage**: idle memory footprint around 25–40 MB and 0% background CPU.
- **Native performance**: fast WPF renderer with hardware acceleration and automatic memory management.
- **Pure offline architecture**: no web views, telemetry, or remote services.

---

## Features

- **Full-window Drag & Drop**: Drop any `.md` or `.txt` file anywhere onto the window to display it instantly.
- **Live File Watcher**: Automatically re-renders content when the source file is modified in any external editor (VS Code, Obsidian, Notepad).
- **CLI Code Copying**: Detects PowerShell, Bash, and terminal code blocks with a dedicated 1-click copy action for terminal use.
- **Link Navigation**: Automatically parses web links (`http://`, `https://`) and Markdown references, opening them in the default browser.
- **Windows System Tray**:
  - Background tray icon with contextual controls.
  - Multi-window note spawning.
  - Show / hide all notes shortcut.
  - Interface language switching (English / Russian).
  - Windows startup toggle (registry Run key).
- **Multi-Notes Management**: Independent note windows with persistent desktop coordinates and sizes.
- **Always-on-Top Toggle**: Pin the note above other windows when executing workflows.
- **Typography Switching**: Switch between proportional and monospace (Cascadia Code / Consolas) typefaces.

---

## Installation & Distribution

Two release builds are available:

1. **Framework-Dependent (~700 KB)**: Ultra-compact binary. Requires [.NET Desktop Runtime 9.0](https://dotnet.microsoft.com/download/dotnet/9.0) installed on the system.
2. **Self-Contained (~65 MB zip)**: Fully standalone archive with embedded .NET 9 runtime. Runs immediately on any clean Windows 10/11 system without dependencies.

Download the latest package from [Releases](https://github.com/Lychqq/NotionDesk/releases).

### Quick Setup

- **Portable**: Extract the archive and launch `NotionDesk.exe`.
- **Install Script**: Run `Install.bat` to deploy to `%LOCALAPPDATA%\Programs\NotionDesk` with Start Menu and Desktop shortcuts.
- **Uninstall**: Run `Uninstall.bat` for complete removal.

---

## Controls

| Element / Shortcut | Action |
| :--- | :--- |
| **File Drag & Drop** | Drop `.md` / `.txt` file anywhere on window |
| **Header Bar** | Hold left mouse button to reposition window |
| **Resize Grip** | Bottom-right corner drag to resize |
| `+` | Open another note window |
| `📁` / File dialog | Select a file via standard Windows Explorer dialog |
| `📝` / Edit | Open active file in system default editor |
| `📌` / Pin | Toggle Always-on-Top |
| `RU / EN` | Switch UI language |
| `Aa` | Switch between proportional and monospace font |
| `✕` | Close note window |
| **Tray Icon** | Double-click to toggle visibility; right-click for full menu |

---

## Build from Source

### Prerequisites

- Windows 10 / 11 (x64)
- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)

```powershell
# Clone repository
git clone https://github.com/Lychqq/NotionDesk.git
cd NotionDesk\DesktopNotes

# Run in development mode
dotnet run

# Build Framework-Dependent binary (~700 KB)
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o "../publish"

# Build Self-Contained standalone package
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -o "../publish_standalone"
```

---

## Project Structure

```text
DesktopNotesWidget/
├── DesktopNotes/
│   ├── App.xaml / App.xaml.cs          # Application lifecycle, tray setup, exception handlers
│   ├── MainWindow.xaml / .cs           # Window layout, drag-and-drop, Markdown/CLI rendering
│   ├── TrayService.cs                  # Windows tray integration and menu management
│   ├── NoteManager.cs                  # Multi-instance window state and persistence
│   ├── LocalizationManager.cs          # Bilingual strings (EN / RU)
│   ├── AppConfig.cs                    # Configuration schema and autostart registration
│   └── DesktopNotes.csproj             # .NET 9 WPF project definition
├── publish/                            # Compiled releases and packaging scripts
├── README.md                           # Documentation (English)
├── README.ru.md                        # Documentation (Russian)
└── ROADMAP.md                          # Technical milestones and planning
```

---

## Roadmap

Planned improvements, architecture milestones, and backlog items are maintained in [ROADMAP.md](ROADMAP.md).

---

## License

This project is licensed under the [MIT License](LICENSE).  
Author: [Nikolay Lvov (@Lychqq)](https://github.com/Lychqq).
