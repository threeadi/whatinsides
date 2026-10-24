using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace WhatInsides.Services;

public static class FileShredder
{
    private const int BUFFER_SIZE = 64 * 1024; // 64 KB

    /// <summary>
    /// Cryptographically overwrites and deletes a file so it cannot be recovered by data recovery software.
    /// </summary>
    public static async Task ShredFileAsync(string filePath, int passes = 1, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath)) return;

        try
        {
            // Remove readonly, hidden, or system attributes if any
            var attributes = File.GetAttributes(filePath);
            if ((attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
            {
                File.SetAttributes(filePath, attributes & ~FileAttributes.ReadOnly);
            }

            long fileLength = new FileInfo(filePath).Length;

            if (fileLength > 0)
            {
                byte[] buffer = new byte[BUFFER_SIZE];

                for (int pass = 0; pass < passes; pass++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Write, FileShare.None);
                    long bytesRemaining = fileLength;

                    while (bytesRemaining > 0)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        int toWrite = (int)Math.Min(buffer.Length, bytesRemaining);

                        if (pass % 2 == 0)
                        {
                            RandomNumberGenerator.Fill(buffer.AsSpan(0, toWrite));
                        }
                        else
                        {
                            Array.Clear(buffer, 0, toWrite);
                        }

                        await fs.WriteAsync(buffer, 0, toWrite, cancellationToken);
                        bytesRemaining -= toWrite;
                    }

                    await fs.FlushAsync(cancellationToken);
                }

                // Truncate file to 0 bytes
                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Write, FileShare.None))
                {
                    fs.SetLength(0);
                    await fs.FlushAsync(cancellationToken);
                }
            }

            // Obfuscate filename in file system directory entries before final delete
            string directory = Path.GetDirectoryName(filePath) ?? string.Empty;
            string randomName = Path.Combine(directory, $"__wi_del_{Guid.NewGuid():N}.tmp");

            try
            {
                File.Move(filePath, randomName);
                File.Delete(randomName);
            }
            catch
            {
                File.Delete(filePath);
            }
        }
        catch (Exception ex)
        {
            // If shredding fails (e.g. locked file), fallback to standard delete
            try
            {
                File.Delete(filePath);
            }
            catch
            {
                throw new IOException($"Gagal menghapus file asli: {ex.Message}", ex);
            }
        }
    }

    /// <summary>
    /// Recursively shreds and deletes an entire directory tree.
    /// </summary>
    public static async Task ShredDirectoryAsync(string dirPath, int passes = 1, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(dirPath)) return;

        string[] files = Directory.GetFiles(dirPath, "*", SearchOption.AllDirectories);
        foreach (var file in files)
        {
            await ShredFileAsync(file, passes, cancellationToken);
        }

        // Delete all empty subdirectories starting from deepest
        var subDirs = Directory.GetDirectories(dirPath, "*", SearchOption.AllDirectories);
        Array.Sort(subDirs, (a, b) => b.Length.CompareTo(a.Length)); // Sort descending by path length
        foreach (var subDir in subDirs)
        {
            try
            {
                Directory.Delete(subDir, true);
            }
            catch { }
        }

        // Delete root directory
        try
        {
            Directory.Delete(dirPath, true);
        }
        catch { }
    }
}
