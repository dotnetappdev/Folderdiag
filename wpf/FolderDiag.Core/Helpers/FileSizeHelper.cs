namespace FolderDiag.Core.Helpers;

public static class FileSizeHelper
{
    private const long KB = 1024L;
    private const long MB = 1024L * KB;
    private const long GB = 1024L * MB;
    private const long TB = 1024L * GB;

    public static string Format(long bytes, int decimals = 2)
    {
        return bytes switch
        {
            < 0 => "0 B",
            < KB => $"{bytes} B",
            < MB => $"{bytes / (double)KB:F{decimals}} KB",
            < GB => $"{bytes / (double)MB:F{decimals}} MB",
            < TB => $"{bytes / (double)GB:F{decimals}} GB",
            _ => $"{bytes / (double)TB:F{decimals}} TB"
        };
    }

    public static string FormatShort(long bytes)
    {
        return bytes switch
        {
            < KB => $"{bytes}B",
            < MB => $"{bytes / (double)KB:F1}K",
            < GB => $"{bytes / (double)MB:F1}M",
            < TB => $"{bytes / (double)GB:F1}G",
            _ => $"{bytes / (double)TB:F1}T"
        };
    }

    public static double ToMB(long bytes) => bytes / (double)MB;
    public static double ToGB(long bytes) => bytes / (double)GB;
}
