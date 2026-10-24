using System;
using System.IO;

namespace WhatInsides.Models;

public class VaultItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string OriginalPath { get; set; } = string.Empty;
    public string EncryptedFileName { get; set; } = string.Empty;
    public bool IsFolder { get; set; }
    public long FileSize { get; set; }
    public DateTime DateLocked { get; set; } = DateTime.Now;
    public string Category { get; set; } = "Other";
    public string OriginalExtension { get; set; } = string.Empty;

    public string FormattedSize => FormatBytes(FileSize);
    public string FormattedDate => DateLocked.ToString("dd MMM yyyy, HH:mm");

    public static string DetectCategory(string path, bool isFolder)
    {
        if (isFolder) return "Folder";
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp" or ".svg" or ".ico" or ".tiff" => "Image",
            ".mp4" or ".mkv" or ".avi" or ".mov" or ".wmv" or ".flv" or ".webm" => "Video",
            ".mp3" or ".wav" or ".flac" or ".aac" or ".ogg" or ".m4a" or ".wma" => "Audio",
            ".pdf" or ".doc" or ".docx" or ".xls" or ".xlsx" or ".ppt" or ".pptx" or ".txt" or ".csv" or ".rtf" or ".md" => "Document",
            ".zip" or ".rar" or ".7z" or ".tar" or ".gz" or ".iso" => "Archive",
            _ => "Other"
        };
    }

    private static string FormatBytes(long bytes)
    {
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
