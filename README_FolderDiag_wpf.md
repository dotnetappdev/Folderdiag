# FolderDiag.wpf (scaffold)

This workspace addition scaffolds a .NET 10 WPF app and supporting libraries:

- `src/FolderDiag.Wpf`: WPF UI targeting `net10.0-windows` (UseWPF)
- `src/FolderDiag.Core`: Core models
- `src/FolderDiag.Data`: EF Core `DbContext` and packages for SQLite, SQL Server, MySQL, and ODBC (Progress 4GL via ODBC)

Quick steps to build and run locally:

```bash
dotnet restore
dotnet build src/FolderDiag.Wpf/FolderDiag.Wpf.csproj
dotnet run --project src/FolderDiag.Wpf/FolderDiag.Wpf.csproj
```

Notes:
- EF Core packages are referenced; run migrations or let `EnsureCreated()` run at startup.
- For MySQL use `Pomelo.EntityFrameworkCore.MySql` and configure `UseMySql()` with server details.
- For Progress 4GL you will likely need an ODBC/ADO.NET provider and configure via `UseOdbc()` or manual ADO.NET.

Next steps I can take:
- Add EF migrations and a sample repository/service layer.
- Implement provider-specific connection factories (MySQL, SQL Server, Progress4GL).
- Create folder-scanning logic and background worker to populate DB.
