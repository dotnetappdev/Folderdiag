using System.ComponentModel.DataAnnotations;

namespace FolderDiag.Data.Entities;

public sealed class SettingEntity
{
    [Key, MaxLength(200)]
    public string Key { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;
}
