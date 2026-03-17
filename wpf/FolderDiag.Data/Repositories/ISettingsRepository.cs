using FolderDiag.Core.Models;

namespace FolderDiag.Data.Repositories;

public interface ISettingsRepository
{
    Task<AppSettings> LoadAsync(CancellationToken ct = default);
    Task SaveAsync(AppSettings settings, CancellationToken ct = default);
    Task<string?> GetValueAsync(string key, CancellationToken ct = default);
    Task SetValueAsync(string key, string value, CancellationToken ct = default);
}
