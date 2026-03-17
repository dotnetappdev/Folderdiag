using System.IO;
using System.Text;
using FolderDiag.Core.Models;

namespace FolderDiag.Wpf.Services;

public static class ExportService
{
    public static async Task ExportToCsvAsync(FileSystemEntry root, string filePath)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Path,Name,Type,Size,TotalSize,% of Parent,Files,Folders,Last Modified,Created");

        void AppendEntry(FileSystemEntry entry, int depth)
        {
            var indent = new string(' ', depth * 2);
            sb.AppendLine(string.Join(",",
                $"\"{entry.FullPath}\"",
                $"\"{indent}{entry.Name}\"",
                entry.IsDirectory ? "Folder" : "File",
                entry.Size,
                entry.TotalSize,
                $"{entry.PercentOfParent:F1}",
                entry.FileCount,
                entry.FolderCount,
                $"\"{entry.LastModified:yyyy-MM-dd HH:mm:ss}\"",
                $"\"{entry.Created:yyyy-MM-dd HH:mm:ss}\""
            ));
            foreach (var child in entry.Children)
                AppendEntry(child, depth + 1);
        }

        AppendEntry(root, 0);
        await File.WriteAllTextAsync(filePath, sb.ToString(), Encoding.UTF8);
    }

    public static async Task ExportToHtmlAsync(FileSystemEntry root, string filePath)
    {
        var sb = new StringBuilder();
        sb.AppendLine("""
            <!DOCTYPE html>
            <html><head>
            <meta charset="UTF-8">
            <title>FolderDiag Report</title>
            <style>
            body { font-family: Segoe UI, sans-serif; background: #1c1c1c; color: #fff; margin: 0; padding: 20px; }
            h1 { color: #60cdff; }
            table { width: 100%; border-collapse: collapse; }
            th { background: #2c2c2c; color: #b4b4b4; padding: 8px; text-align: left; font-weight: 600; }
            td { padding: 6px 8px; border-bottom: 1px solid #2d2d2d; }
            tr:hover { background: #323232; }
            .bar { height: 8px; background: #0078d4; border-radius: 4px; }
            .folder { color: #60cdff; }
            .size { text-align: right; font-family: Cascadia Mono, Consolas, monospace; }
            </style></head><body>
            """);
        sb.AppendLine($"<h1>FolderDiag Report: {root.FullPath}</h1>");
        sb.AppendLine($"<p>Scanned: {DateTime.Now:yyyy-MM-dd HH:mm:ss} &mdash; Total: {root.TotalSizeFormatted} in {root.FileCount:N0} files</p>");
        sb.AppendLine("<table><thead><tr><th>Name</th><th>Size</th><th>% of Parent</th><th>Files</th><th>Modified</th></tr></thead><tbody>");

        void AppendEntry(FileSystemEntry entry, int depth)
        {
            var indent = new string('&nbsp;', depth * 4);
            var icon = entry.IsDirectory ? "📁" : "📄";
            var nameClass = entry.IsDirectory ? "folder" : "";
            sb.AppendLine($"""
                <tr>
                  <td>{indent}{icon} <span class="{nameClass}">{entry.Name.Replace("&","&amp;").Replace("<","&lt;").Replace(">","&gt;")}</span></td>
                  <td class="size">{entry.TotalSizeFormatted}</td>
                  <td>
                    <div class="bar" style="width:{Math.Min(100, entry.PercentOfParent):F0}%"></div>
                    {entry.PercentOfParent:F1}%
                  </td>
                  <td class="size">{(entry.IsDirectory ? entry.FileCount.ToString("N0") : "")}</td>
                  <td>{entry.LastModified:yyyy-MM-dd}</td>
                </tr>
                """);
            foreach (var child in entry.Children.Take(100))
                AppendEntry(child, depth + 1);
        }

        AppendEntry(root, 0);
        sb.AppendLine("</tbody></table></body></html>");
        await File.WriteAllTextAsync(filePath, sb.ToString(), Encoding.UTF8);
    }
}
