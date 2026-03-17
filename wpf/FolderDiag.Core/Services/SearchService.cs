using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using FolderDiag.Core.Models;
using Microsoft.Extensions.Logging;

namespace FolderDiag.Core.Services;

public sealed class SearchService : ISearchService
{
    private readonly ILogger<SearchService> _logger;

    public SearchService(ILogger<SearchService> logger)
    {
        _logger = logger;
    }

    public IAsyncEnumerable<FileSystemEntry> SearchAsync(SearchQuery query, CancellationToken ct = default)
        => SearchLiveAsync(query.SearchPath, query, ct);

    public async IAsyncEnumerable<FileSystemEntry> SearchLiveAsync(
        string rootPath,
        SearchQuery query,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
            yield break;

        Regex? regex = null;
        if (query.UseRegex && !string.IsNullOrEmpty(query.Text))
        {
            var opts = query.MatchCase ? RegexOptions.None : RegexOptions.IgnoreCase;
            try { regex = new Regex(query.Text, opts | RegexOptions.Compiled); }
            catch { yield break; }
        }

        var searchPattern = query.UseRegex ? "*" : BuildWildcardPattern(query.Text);
        var comparison = query.MatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

        int count = 0;
        var stack = new Stack<string>();
        stack.Push(rootPath);

        while (stack.Count > 0 && count < query.MaxResults)
        {
            ct.ThrowIfCancellationRequested();
            var dir = stack.Pop();

            if (query.IncludeFolders)
            {
                string[] subdirs = [];
                try { subdirs = Directory.GetDirectories(dir); }
                catch { }

                foreach (var sub in subdirs)
                {
                    ct.ThrowIfCancellationRequested();
                    var name = Path.GetFileName(sub);
                    if (Matches(name, query.Text, regex, comparison, query.UseRegex))
                    {
                        var entry = CreateEntry(sub, true);
                        if (MatchesFilters(entry, query))
                        {
                            yield return entry;
                            if (++count >= query.MaxResults) yield break;
                        }
                    }
                    stack.Push(sub);
                }
            }
            else
            {
                try
                {
                    foreach (var sub in Directory.GetDirectories(dir))
                        stack.Push(sub);
                }
                catch { }
            }

            if (query.IncludeFiles)
            {
                string[] files = [];
                try { files = Directory.GetFiles(dir); }
                catch { }

                foreach (var file in files)
                {
                    ct.ThrowIfCancellationRequested();
                    var name = Path.GetFileName(file);
                    if (Matches(name, query.Text, regex, comparison, query.UseRegex))
                    {
                        var entry = CreateEntry(file, false);
                        if (MatchesFilters(entry, query))
                        {
                            yield return entry;
                            if (++count >= query.MaxResults) yield break;
                        }
                    }
                }
            }

            // Yield to allow UI to update
            await Task.Yield();
        }
    }

    private static bool Matches(string name, string text, Regex? regex, StringComparison comparison, bool useRegex)
    {
        if (string.IsNullOrEmpty(text)) return true;
        if (useRegex && regex != null) return regex.IsMatch(name);
        // Support * wildcard
        if (text.Contains('*') || text.Contains('?'))
            return MatchWildcard(name, text, comparison == StringComparison.OrdinalIgnoreCase);
        return name.Contains(text, comparison);
    }

    private static bool MatchWildcard(string text, string pattern, bool ignoreCase)
    {
        if (ignoreCase) { text = text.ToLowerInvariant(); pattern = pattern.ToLowerInvariant(); }
        int ti = 0, pi = 0, star = -1, match = 0;
        while (ti < text.Length)
        {
            if (pi < pattern.Length && (pattern[pi] == '?' || pattern[pi] == text[ti]))
            { ti++; pi++; }
            else if (pi < pattern.Length && pattern[pi] == '*')
            { star = pi++; match = ti; }
            else if (star >= 0)
            { pi = star + 1; ti = ++match; }
            else return false;
        }
        while (pi < pattern.Length && pattern[pi] == '*') pi++;
        return pi == pattern.Length;
    }

    private static bool MatchesFilters(FileSystemEntry entry, SearchQuery query)
    {
        if (query.Extensions?.Length > 0 && !entry.IsDirectory)
        {
            if (!query.Extensions.Any(e => e.Equals(entry.Extension, StringComparison.OrdinalIgnoreCase)))
                return false;
        }
        if (query.MinSize.HasValue && entry.Size < query.MinSize.Value) return false;
        if (query.MaxSize.HasValue && entry.Size > query.MaxSize.Value) return false;
        if (query.ModifiedAfter.HasValue && entry.LastModified < query.ModifiedAfter.Value) return false;
        if (query.ModifiedBefore.HasValue && entry.LastModified > query.ModifiedBefore.Value) return false;
        return true;
    }

    private static FileSystemEntry CreateEntry(string path, bool isDir)
    {
        var entry = new FileSystemEntry
        {
            FullPath = path,
            Name = Path.GetFileName(path),
            IsDirectory = isDir,
            ScanComplete = true
        };
        try
        {
            if (isDir)
            {
                var di = new DirectoryInfo(path);
                entry.Created = di.CreationTime;
                entry.LastModified = di.LastWriteTime;
            }
            else
            {
                var fi = new FileInfo(path);
                entry.Size = fi.Length;
                entry.TotalSize = fi.Length;
                entry.Extension = fi.Extension.ToLowerInvariant();
                entry.Created = fi.CreationTime;
                entry.LastModified = fi.LastWriteTime;
            }
        }
        catch { }
        return entry;
    }

    private static string BuildWildcardPattern(string text) =>
        string.IsNullOrEmpty(text) ? "*" : $"*{text}*";
}
