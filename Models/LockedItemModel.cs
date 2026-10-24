using System;
using System.IO;

namespace WhatInsides.Models;

public class LockedItemModel
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string OriginalPath { get; set; } = string.Empty;
    public string CurrentPath { get; set; } = string.Empty;
    public string LockType { get; set; } = "FastHide"; // "FastHide" (Mode B) or "Aes256" (Mode C)
    public bool IsFolder { get; set; }
    public long FileSize { get; set; }
    public DateTime DateLocked { get; set; } = DateTime.Now;
    public bool IsLocked { get; set; } = true;
    public string? TempPreviewPath { get; set; }

    public string DriveLetter => !string.IsNullOrEmpty(OriginalPath) && OriginalPath.Length >= 2 ? OriginalPath.Substring(0, 2).ToUpper() : "-";
    public string LockTypeBadge => LockType == "FastHide" ? "⚡ Kunci Cepat" : "🛡️ AES-256";
    public string StatusBadge => IsLocked ? "Terkunci" : "Terbuka";
    public string FormattedSize => FormatBytes(FileSize);
    public string FormattedDate => DateLocked.ToString("dd MMM yyyy, HH:mm");

    public static string DetectCategory(string path, bool isFolder)
    {
        if (isFolder) return "Folder";
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp" => "Image",
            ".mp4" or ".mkv" or ".avi" or ".mov" or ".wmv" => "Video",
            ".pdf" or ".doc" or ".docx" or ".xls" or ".xlsx" or ".ppt" or ".pptx" or ".txt" => "Document",
            _ => "File"
        };
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes <= 0) return "0 B";
        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        int counter = 0;
        decimal number = bytes;
        while (Math.Round(number / 1024) >= 1)
        {
            number /= 1024;
            counter++;
            if (counter >= suffixes.Length - 1) break;
        }
        return $"{number:n1} {suffixes[counter]}";
    }
}
