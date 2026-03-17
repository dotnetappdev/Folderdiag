using System.Text.Json;
using FolderDiag.Core.Models;
using FolderDiag.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace FolderDiag.Data.Repositories;

public sealed class SettingsRepository : ISettingsRepository
{
    private readonly AppDbContext _db;
    private const string SettingsKey = "AppSettings";

    public SettingsRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<AppSettings> LoadAsync(CancellationToken ct = default)
    {
        var entity = await _db.Settings.FindAsync([SettingsKey], ct);
        if (entity?.Value is { } json)
        {
            try
            {
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
            catch { }
        }
        return new AppSettings();
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(settings);
        var entity = await _db.Settings.FindAsync([SettingsKey], ct);
        if (entity == null)
        {
            _db.Settings.Add(new SettingEntity { Key = SettingsKey, Value = json });
        }
        else
        {
            entity.Value = json;
            _db.Settings.Update(entity);
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task<string?> GetValueAsync(string key, CancellationToken ct = default)
    {
        var entity = await _db.Settings.FindAsync([key], ct);
        return entity?.Value;
    }

    public async Task SetValueAsync(string key, string value, CancellationToken ct = default)
    {
        var entity = await _db.Settings.FindAsync([key], ct);
        if (entity == null)
            _db.Settings.Add(new SettingEntity { Key = key, Value = value });
        else
        {
            entity.Value = value;
            _db.Settings.Update(entity);
        }
        await _db.SaveChangesAsync(ct);
    }
}
