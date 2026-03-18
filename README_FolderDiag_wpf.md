# FolderDiag — Windows 11 File Explorer + Folder Size Analyzer

A full-featured WPF desktop application that combines a **1:1 Windows 11 File Explorer clone** with all features from [FolderSizes](https://www.foldersizes.com/features).

---

## UI Layout

```
┌──────────────────────────────────────────────────────────────────────────┐
│  📁 FolderDiag  │  [Home] [View] [Reports]  ← Ribbon Tabs               │
│─────────────────────────────────────────────────────────────────────────│
│  [Ribbon Bar: Scan | Stop | Refresh | Copy | Move | Delete | Zip |       │
│               CSV | XML | HTML | Snapshot | Compare | Schedule]          │
│─────────────────────────────────────────────────────────────────────────│
│  [←] [→] [↑]  [ Address Bar: C:\Users\…         ]  [ 🔍 Search…  ]      │
│─────────────────────────────────────────────────────────────────────────│
│  ▾ Quick Access     │  [📁 Folder Sizes][📂 File Browser][💽 Disk Space] │
│    📌 Desktop        │  [📋 File Reports][🗂 Classification][🔍 Search]   │
│    ⬇ Downloads       │  [📷 Snapshots]                                    │
│    📄 Documents      │                                                    │
│    🖼 Pictures        │  Name  │ Size │ Bar │ % │ Alloc │ Files │ Owner   │
│  ▾ This PC           │  ──────┼──────┼─────┼───┼───────┼───────┼──────  │
│    💽 C: (Windows)   │  📁 …  │ 12GB │ ██  │55%│ 14GB  │ 1,234 │ SYSTEM │
│    💾 D: (Data)      │  📁 …  │  4GB │ ██  │18%│  5GB  │   456 │ …      │
│  ▾ Network           │────────────────────────────────────────────────── │
│    🌐 \\server\share  │  [Bar Chart / Pie Chart / Treemap]  │ Details Pane│
│  ▾ Cloud Storage     │                                      │ Name: …    │
│    ☁ OneDrive        │                                      │ Size: …    │
│    ☁ Google Drive    │                                      │ Files: …   │
│    ☁ Dropbox         │                                      │ Owner: …   │
│─────────────────────────────────────────────────────────────────────────│
│  234 folders  │  12,456 files | 890 sub-folders  │  Total: 45.2 GB      │
└──────────────────────────────────────────────────────────────────────────┘
```

---

## Features

### Windows 11 File Explorer (1:1 Clone)
| Feature | Description |
|---|---|
| Navigation pane | Quick Access, This PC (all drives), Network, Cloud Storage |
| Quick Access | Desktop, Downloads, Documents, Pictures, Music, Videos + pinned folders |
| Pin to Quick Access | Right-click any folder → "Pin to Quick Access"; persists across sessions |
| Drives | All local, removable, CD/DVD, and network drives with correct icons |
| Cloud Storage | Auto-detects OneDrive, Google Drive, Dropbox, iCloud, Box |
| Network drives | Listed under Network section |
| Address bar | Type any path and press Enter to navigate |
| Back / Forward / Up | Full navigation history |
| Search bar | Live filter on folder names |
| Context menus | Open in Explorer, Scan, Pin, Copy Path, Copy, Move, Zip, Delete, Properties |
| Hidden/System files | Toggle Show Hidden / Show System / Show Extensions from View ribbon |

### FolderSizes Features
| Feature | Description |
|---|---|
| Folder Sizes tab | Sortable grid: Name, Size, Bar chart, %, Allocated, Files, Folders, Modified, Owner, Path |
| Bar chart | Clickable colour bars showing % of parent (green/yellow/red by usage) |
| File Browser tab | Full directory listing: folders + files with type icons, size, dates, owner |
| Disk Space tab | Drive cards with usage bars (blue/orange/red), Used/Free/Total, filesystem |
| Bar Chart | Scrollable bar chart for top 20 folders (click to drill in) |
| Pie Chart | Proportional pie chart with legend |
| Treemap | Proportional treemap; click segment to navigate |
| File Reports | Largest, Oldest, Newest, Temp files, Duplicates by size, By Category, By Owner, By Age |
| Classification tab | Grouping by file category (Images, Video, Audio, Documents, etc.) with pie chart |
| Advanced Search | Filter by name, extension, category, min size, modified date, owner |
| CSV Export | Full folder data with all columns |
| XML Export | Structured XML report |
| HTML Export | Self-contained HTML report with styled table and inline bars |
| Snapshot | Save scan state (name, root, date, total size/files/folders) |
| Compare Snapshots | Side-by-side diff of two snapshots (Grew/Shrank/New/Deleted + colour coding) |
| Copy folder | Copy selected folder to destination |
| Move folder | Move selected folder to destination |
| Delete | Sends to Recycle Bin (safe) |
| ZIP compression | Compress selected folder to ZIP |
| Open in Explorer | Launch Windows Explorer at selected path |
| File owner | Reads Windows ACL owner for files and folders |
| Scan with cancel | Async recursive scan with Stop button and cancellation token |
| Depth filter | View 1 / 2 / 3 levels or All |
| Min-size filter | Filter folders by minimum size |

---

## Architecture

```
FolderDiag.sln
├── src/FolderDiag.Core/
│   └── Models/
│       ├── FolderItem.cs       — folder scan result + formatting helpers
│       ├── FileItem.cs         — individual file with category/age helpers
│       └── ScanSnapshot.cs     — snapshot for trend comparison
├── src/FolderDiag.Data/
│   └── FolderDiagDbContext.cs  — EF Core context (SQLite; FolderItems, FileItems, ScanSnapshots)
└── src/FolderDiag.Wpf/
    ├── Converters/Converters.cs — BytesToString, PercentToBar, PercentToColor, DateTimeFormat, etc.
    ├── Themes/ModernTheme.xaml  — Full Windows 11 light theme (buttons, grids, trees, tabs, scrollbars)
    ├── MainWindow.xaml          — Complete UI (ribbon, nav pane, 7 content tabs, charts, details)
    └── MainWindow.xaml.cs       — All logic: scanning, charts, reports, search, snapshots, file ops
```

---

## Build & Run

> **Requires:** Windows 10/11, .NET 10 SDK

```powershell
cd src/FolderDiag.Wpf
dotnet restore
dotnet run
```

Or build the full solution:

```powershell
dotnet build FolderDiag.wpf.sln
```

---

## Keyboard Shortcuts

| Key | Action |
|---|---|
| `Alt + Left` | Back |
| `Alt + Right` | Forward |
| `Alt + Up` | Up one level |
| `Enter` (address bar) | Navigate to typed path |
| `Ctrl + F` | Focus search box |
| `F5` | Refresh |

---

## Data Storage

Scan data is stored in `folderdiag.db` (SQLite) next to the executable.  
Pinned Quick Access paths are stored in `pinned.txt` next to the executable.

