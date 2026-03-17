using FolderDiag.Core.Models;
using FolderDiag.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace FolderDiag.Data.Repositories;

public sealed class ScanHistoryRepository : IScanHistoryRepository
{
    private readonly AppDbContext _db;

    public ScanHistoryRepository(AppDbContext db) { _db = db; }

    public async Task<ScanHistoryEntity> SaveScanAsync(ScanResult result, CancellationToken ct = default)
    {
        var entity = new ScanHistoryEntity
        {
            RootPath = result.RootPath,
            ScanTime = result.ScanTime,
            TotalSize = result.TotalSize,
            TotalFiles = result.TotalFiles,
            TotalFolders = result.TotalFolders,
            ScanDurationSeconds = result.ScanDuration.TotalSeconds,
            IsComplete = result.IsComplete,
            ErrorMessage = result.ErrorMessage
        };
        _db.ScanHistory.Add(entity);
        await _db.SaveChangesAsync(ct);
        return entity;
    }

    public async Task<List<ScanHistoryEntity>> GetRecentScansAsync(int count = 20, CancellationToken ct = default)
        => await _db.ScanHistory
            .OrderByDescending(x => x.ScanTime)
            .Take(count)
            .ToListAsync(ct);

    public async Task DeleteScanAsync(int id, CancellationToken ct = default)
    {
        var entity = await _db.ScanHistory.FindAsync([id], ct);
        if (entity != null)
        {
            _db.ScanHistory.Remove(entity);
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task<List<ScanHistoryEntity>> GetScansByPathAsync(string path, CancellationToken ct = default)
        => await _db.ScanHistory
            .Where(x => x.RootPath == path)
            .OrderByDescending(x => x.ScanTime)
            .ToListAsync(ct);
}
