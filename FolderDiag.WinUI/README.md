# FolderDiag.WinUI

**A Windows 11 File Explorer clone with full FolderSizes analysis built on the [Files](https://github.com/files-community/Files) open-source codebase.**

FolderDiag.WinUI takes the complete Files app (the best-in-class Windows file manager) and layers every feature from [FolderSizes](https://www.foldersizes.com/features) directly into the existing UI:

- Extra **details-view columns** (size bar, %, file count, folder count, allocated size)
- A dedicated **Folder Analysis side pane** (bar chart, reports, disk space bars, file classification, snapshots)
- **Per-drive disk space mini-bars** in the status bar
- **System / Light / Dark theme** toggle in the toolbar

---

## Architecture

```
FolderDiag.WinUI/                     ← root (direct copy of Files repo)
├── FolderDiag.WinUI.slnx             ← solution (renamed from Files.slnx)
├── src/
│   ├── Files.App/                    ← main WinUI 3 app (all original Files code)
│   │   ├── Data/
│   │   │   ├── Items/ListedItem.cs   ← +FolderSizeBytes, FolderFileCount,
│   │   │   │                             FolderSubfolderCount, FolderAllocatedBytes,
│   │   │   │                             FolderSizePercent, IsFolderSizeLoading
│   │   │   ├── Messages/
│   │   │   │   └── FolderDiagMessages.cs    ← FolderDiagAnalysisPaneMessage
│   │   ├── Services/
│   │   │   └── FolderSizes/
│   │   │       ├── IFolderSizeService.cs    ← interface + event args
│   │   │       └── FolderSizeService.cs     ← recursive async scanner
│   │   ├── Data/Models/FolderSizes/
│   │   │   └── FolderSizeResult.cs          ← FolderSizeResult, FileReportItem,
│   │   │                                        TypeClassificationItem, DriveSpaceItem
│   │   ├── UserControls/
│   │   │   ├── FolderAnalysis/
│   │   │   │   ├── FolderAnalysisPane.xaml  ← full analysis pane UI
│   │   │   │   └── FolderAnalysisPane.xaml.cs
│   │   │   ├── StatusBar.xaml               ← +drive space mini-bars
│   │   │   └── Toolbar.xaml                 ← +Folder Analysis toggle, Theme picker
│   │   ├── ViewModels/FolderAnalysis/
│   │   │   └── FolderAnalysisPaneViewModel.cs ← MVVM ViewModel
│   │   └── Views/Layouts/
│   │       └── DetailsLayoutPage.xaml       ← +FolderDiag size columns injected
│   └── [all other Files.App projects unchanged]
```

---

## FolderDiag Additions

### Details View — New Columns (folders only)

| Column | Description |
|---|---|
| **Size Bar** | Inline `ProgressBar` (0–100 %) + human-readable size below it |
| **%** | Percentage of parent folder's total size |
| **Files** | Total recursive file count |
| **Subfolders** | Total recursive subfolder count |
| **Allocated** | Cluster-aligned size on disk |

Columns appear automatically for folder rows; file rows keep the standard `FileSize` column.

### Folder Analysis Pane

Toggle via **toolbar → 📊 Folder Analysis** button.

| Tab | Contents |
|---|---|
| **Sizes** | Sorted list of subfolders with inline progress bars + bar chart strip |
| **Reports** | Largest / Oldest / Temp / Duplicate files with one-click runner |
| **Disk** | Card per drive: usage bar (blue → orange → red), Used / Free / Total |
| **Types** | Files grouped by category (Images, Video, Audio, Documents, Archives, Executables, Source Code, Temporary, Config, Text) with progress bars |
| **Snapshots** | Save/view scan snapshots for trend comparison |

### Status Bar — Drive Space Mini-bars

One compact chip per ready drive: `C:  ████░░  45%`
Bars tint **orange** at > 70% and **red** at > 85% full.

### Theme Toggle

Toolbar → ☀ **Theme** flyout:
- **System default** — follows Windows accent/theme
- **Light** — forces `ElementTheme.Light`
- **Dark** — forces `ElementTheme.Dark`

Persisted via Files' existing `IAppearanceSettingsService`.

---

## Building

Requires Windows 10/11 (build 19041+), Visual Studio 2022 with Windows App SDK workload, .NET 10 SDK.

```powershell
cd FolderDiag.WinUI
dotnet restore
# Open FolderDiag.WinUI.slnx in Visual Studio and run Files.App
```

Or via CLI (x64 debug):
```powershell
dotnet build src/Files.App/Files.App.csproj -p:Platform=x64
```

---

## Credits

- **UI base**: [Files app](https://github.com/files-community/Files) (MIT) — the best open-source Windows file manager
- **FolderSizes features**: [foldersizes.com](https://www.foldersizes.com/features) for feature inspiration
- **FolderDiag additions**: MIT license

