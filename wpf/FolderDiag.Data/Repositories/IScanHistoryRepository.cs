using FolderDiag.Core.Models;
using FolderDiag.Data.Entities;

namespace FolderDiag.Data.Repositories;

public interface IScanHistoryRepository
{
    Task<ScanHistoryEntity> SaveScanAsync(ScanResult result, CancellationToken ct = default);
    Task<List<ScanHistoryEntity>> GetRecentScansAsync(int count = 20, CancellationToken ct = default);
    Task DeleteScanAsync(int id, CancellationToken ct = default);
    Task<List<ScanHistoryEntity>> GetScansByPathAsync(string path, CancellationToken ct = default);
}
