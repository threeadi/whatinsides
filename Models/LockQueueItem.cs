using System;
using System.IO;

namespace WhatInsides.Models;

public class LockQueueItem
{
    public string Path { get; set; } = string.Empty;
    public string Name => System.IO.Path.GetFileName(Path);
    public string DirectoryPath => System.IO.Path.GetDirectoryName(Path) + "\\";
    public bool IsFolder { get; set; }
    public long SizeBytes { get; set; }
    public string FormattedSize => IsFolder ? "Direktori" : FormatBytes(SizeBytes);
    public string Category => LockedItemModel.DetectCategory(Path, IsFolder);

    public static LockQueueItem FromPath(string path)
    {
        bool isFolder = Directory.Exists(path);
        long size = 0;
        try
        {
            if (isFolder)
            {
                var dirInfo = new DirectoryInfo(path);
                foreach (var file in dirInfo.EnumerateFiles("*", SearchOption.AllDirectories))
                {
                    size += file.Length;
                }
            }
            else if (File.Exists(path))
            {
                size = new FileInfo(path).Length;
            }
        }
        catch { }

        return new LockQueueItem
        {
            Path = path,
            IsFolder = isFolder,
            SizeBytes = size
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
