using FolderDiag.Core.Models;

namespace FolderDiag.Core.Services;

public interface ISearchService
{
    IAsyncEnumerable<FileSystemEntry> SearchAsync(SearchQuery query, CancellationToken ct = default);

    IAsyncEnumerable<FileSystemEntry> SearchLiveAsync(
        string rootPath,
        SearchQuery query,
        CancellationToken ct = default);
}
